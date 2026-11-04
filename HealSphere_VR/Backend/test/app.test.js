import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, readFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { createApp, CAT_PROMPT } from '../src/app.js';
import { validateGlb } from '../src/glb.js';

const token = 'local-test-capability-0123456789-abcdef';
function fixture(modify = () => {}) {
  const doc = { asset: { version: '2.0' }, scene: 0, scenes: [{ nodes: [0] }], nodes: [{ mesh: 0 }],
    meshes: [{ primitives: [{ attributes: { POSITION: 0 } }] }],
    accessors: [{ bufferView: 0, componentType: 5126, count: 3, type: 'VEC3', min: [0, 0, 0], max: [1, 1, 0] }],
    bufferViews: [{ buffer: 0, byteLength: 36 }], buffers: [{ byteLength: 36 }] };
  modify(doc);
  const text = JSON.stringify(doc);
  const json = Buffer.from(text.padEnd(Math.ceil(Buffer.byteLength(text) / 4) * 4, ' '));
  const bin = Buffer.alloc(36); bin.writeFloatLE(1, 12); bin.writeFloatLE(1, 28);
  const bytes = Buffer.alloc(28 + json.length + bin.length);
  bytes.writeUInt32LE(0x46546c67, 0); bytes.writeUInt32LE(2, 4); bytes.writeUInt32LE(bytes.length, 8);
  bytes.writeUInt32LE(json.length, 12); bytes.writeUInt32LE(0x4e4f534a, 16); json.copy(bytes, 20);
  bytes.writeUInt32LE(bin.length, 20 + json.length); bytes.writeUInt32LE(0x004e4942, 24 + json.length); bin.copy(bytes, 28 + json.length);
  return bytes;
}
const ok = data => Response.json({ code: 0, data });
async function setup(t, options = {}) {
  const dataDir = await mkdtemp(path.join(tmpdir(), 'tripo-test-'));
  const instance = await createApp({ key: 'fake-key-never-real', token, enabled: true, dataDir, pollMs: 1, ...options });
  const server = instance.app.listen(0, '127.0.0.1');
  await new Promise(resolve => server.once('listening', resolve));
  t.after(async () => { await instance.waitForWorker(); await new Promise(resolve => server.close(resolve)); });
  const request = (route, body, extra = {}) => fetch(`http://127.0.0.1:${server.address().port}${route}`, {
    method: body ? 'POST' : 'GET', headers: { 'X-Backend-Token': token, 'Content-Type': 'application/json', ...extra },
    ...(body ? { body: JSON.stringify(body) } : {})
  });
  return { ...instance, request, dataDir };
}
test('balance and connection are free; protected endpoints require local authorization', async t => {
  const calls = [];
  const { request } = await setup(t, { fetchFn: async (url, opts) => { calls.push([url, opts]); return ok({ balance: 42, frozen: 1 }); } });
  assert.equal((await request('/health', null, { 'X-Backend-Token': '' })).status, 200);
  assert.equal((await request('/api/balance', null, { 'X-Backend-Token': '' })).status, 401);
  assert.equal((await request('/api/test-cat', { confirm: 'GENERATE_ONE_CAT' }, { Origin: 'https://hostile.example' })).status, 403);
  assert.deepEqual(await (await request('/api/balance')).json(), { balance: 42, frozen: 1 });
  assert.equal(calls.length, 1); assert.ok(calls[0][0].endsWith('/account/balance'));
});
test('one paid submission, async polling, bounded GLB download, review gate, idempotence and restart', async t => {
  let posts = 0, polls = 0;
  const bytes = fixture();
  const upstream = async (url, opts) => {
    url = String(url);
    if (url.endsWith('/account/balance')) return ok({ balance: 100, frozen: 0 });
    if (url.endsWith('/generation/text-to-model')) {
      posts++;
      const payload = JSON.parse(opts.body);
      assert.equal(payload.model, 'P1-20260311'); assert.equal(payload.prompt, CAT_PROMPT);
      return ok({ task_id: 'test-task' });
    }
    if (url.endsWith('/tasks/test-task')) {
      polls++;
      return ok({ task_id: 'test-task', status: polls < 2 ? 'running' : 'success', progress: 50,
        output: { model_url: 'https://cdn.tripo3d.ai/test.glb?private-signature=never-return' } });
    }
    assert.equal(opts.headers, undefined);
    return new Response(bytes);
  };
  const s = await setup(t, { fetchFn: upstream });
  assert.equal((await s.request('/api/test-cat/model')).status, 409);
  assert.equal((await s.request('/api/test-cat', { confirm: 'GENERATE_ONE_CAT' })).status, 202);
  await s.waitForWorker();
  const state = await (await s.request('/api/test-cat')).json();
  assert.equal(state.status, 'awaiting_review'); assert.equal(state.task_id, 'test-task');
  assert.ok(!JSON.stringify(state).includes('private-signature'));
  assert.equal((await s.request('/api/test-cat/model')).status, 409);
  assert.equal((await s.request('/api/test-cat/review', { decision: 'APPROVE' })).status, 200);
  assert.deepEqual(Buffer.from(await (await s.request('/api/test-cat/model')).arrayBuffer()), bytes);
  await s.request('/api/test-cat', { confirm: 'GENERATE_ONE_CAT' });
  assert.equal(posts, 1); assert.equal(polls, 2);
  const restarted = await createApp({ key: 'fake', token, enabled: true, dataDir: s.dataDir, fetchFn: () => assert.fail('restart must reuse completed job') });
  assert.ok(restarted.app);
  assert.equal(JSON.parse(await readFile(path.join(s.dataDir, 'test-cat.json'))).status, 'ready');
});
test('ambiguous paid POST is journaled and is never retried, including after restart', async t => {
  let posts = 0;
  const { request, dataDir } = await setup(t, { fetchFn: async (url) => {
    if (url.endsWith('/account/balance')) return ok({ balance: 100, frozen: 0 });
    posts++; throw new Error('contains fake upstream secret');
  } });
  const first = await request('/api/test-cat', { confirm: 'GENERATE_ONE_CAT' });
  assert.equal(first.status, 502); assert.ok(!(await first.text()).includes('secret'));
  assert.equal((await request('/api/test-cat', { confirm: 'GENERATE_ONE_CAT' })).status, 409);
  await createApp({ key: 'fake', token, enabled: true, dataDir, fetchFn: () => assert.fail('must not resubmit') });
  assert.equal(posts, 1);
});
test('missing opt-in, insufficient credits and authentication failures do not submit tasks', async t => {
  for (const variant of ['disabled', 'empty', 'auth']) {
    const s = await setup(t, { enabled: variant !== 'disabled', fetchFn: async url => {
      assert.ok(url.endsWith('/account/balance'));
      return variant === 'auth' ? Response.json({ code: 1002, message: 'secret' }, { status: 401 }) : ok({ balance: 0, frozen: 0 });
    } });
    const response = await s.request('/api/test-cat', { confirm: 'GENERATE_ONE_CAT' });
    assert.equal(response.status, variant === 'disabled' ? 403 : variant === 'empty' ? 402 : 502);
  }
});
test('timeout and terminal upstream failures stop polling without resubmission', async t => {
  for (const variant of ['timeout', 'banned']) {
    let posts = 0;
    const s = await setup(t, { taskTimeoutMs: variant === 'timeout' ? 2 : 1000, fetchFn: async url => {
      if (url.endsWith('/account/balance')) return ok({ balance: 100, frozen: 0 });
      if (url.endsWith('/generation/text-to-model')) { posts++; return ok({ task_id: 'task' }); }
      return ok({ task_id: 'task', status: variant === 'timeout' ? 'running' : 'banned' });
    } });
    await s.request('/api/test-cat', { confirm: 'GENERATE_ONE_CAT' }); await s.waitForWorker();
    const state = await (await s.request('/api/test-cat')).json();
    assert.equal(state.status, 'failed'); assert.equal(state.error, variant === 'timeout' ? 'TASK_TIMEOUT' : 'TASK_BANNED'); assert.equal(posts, 1);
  }
});
test('reject malformed GLBs, external URLs, overbudget geometry and materials', async () => {
  assert.equal((await validateGlb(fixture())).triangles, 1);
  await assert.rejects(validateGlb(Buffer.from('not a GLB')));
  for (const mutate of [d => { d.buffers[0].uri = 'https://evil.example/payload'; },
    d => { d.materials = Array(9).fill({}); }, d => { d.accessors[0].count = 60003; },
    d => { d.extensionsUsed = ['KHR_draco_mesh_compression']; }]) {
    await assert.rejects(validateGlb(fixture(mutate)));
  }
});
test('untrusted CDN host is rejected before fetching the model', async t => {
  const s = await setup(t, { fetchFn: async url => {
    if (url.endsWith('/account/balance')) return ok({ balance: 100, frozen: 0 });
    if (url.endsWith('/generation/text-to-model')) return ok({ task_id: 'task' });
    assert.ok(url.endsWith('/tasks/task'));
    return ok({ task_id: 'task', status: 'success', output: { model_url: 'http://127.0.0.1/private' } });
  } });
  await s.request('/api/test-cat', { confirm: 'GENERATE_ONE_CAT' }); await s.waitForWorker();
  assert.equal((await (await s.request('/api/test-cat')).json()).error, 'MODEL_HOST_REJECTED');
});
test('transient polling network failures recover without another paid POST', async t => {
  let polls = 0, posts = 0;
  const s = await setup(t, { fetchFn: async url => {
    if (String(url).endsWith('/account/balance')) return ok({ balance: 100, frozen: 0 });
    if (String(url).endsWith('/generation/text-to-model')) { posts++; return ok({ task_id: 'task' }); }
    if (String(url).endsWith('/tasks/task')) {
      if (++polls === 1) throw new Error('temporary disconnect');
      return ok({ task_id: 'task', status: 'success', output: { model_url: 'https://cdn.tripo3d.ai/cat.glb' } });
    }
    return new Response(fixture());
  } });
  await s.request('/api/test-cat', { confirm: 'GENERATE_ONE_CAT' }); await s.waitForWorker();
  assert.equal((await (await s.request('/api/test-cat')).json()).status, 'awaiting_review');
  assert.equal(posts, 1); assert.equal(polls, 2);
});
test('malformed upstream response fails safely without leaking its content', async t => {
  const s = await setup(t, { fetchFn: async () => new Response('private upstream diagnostic') });
  const response = await s.request('/api/balance');
  assert.equal(response.status, 502);
  assert.deepEqual(await response.json(), { error: 'UPSTREAM_NETWORK_OR_INVALID_RESPONSE' });
});
