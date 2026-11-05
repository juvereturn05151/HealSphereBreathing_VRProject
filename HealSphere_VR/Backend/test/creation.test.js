import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, readFile, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { createApp } from '../src/app.js';
import { offlineInterpret, validateInterpretation } from '../src/conversation.js';
const token = 'creation-tests-local-capability-0123456789';
const ok = data => Response.json({ code: 0, data });
function glb() {
  const doc = { asset: { version: '2.0' }, scene: 0, scenes: [{ nodes: [0] }], nodes: [{ mesh: 0 }], meshes: [{ primitives: [{ attributes: { POSITION: 0 } }] }], accessors: [{ bufferView: 0, componentType: 5126, count: 3, type: 'VEC3', min: [0, 0, 0], max: [1, 1, 0] }], bufferViews: [{ buffer: 0, byteLength: 36 }], buffers: [{ byteLength: 36 }] };
  const json = Buffer.from(JSON.stringify(doc).padEnd(Math.ceil(Buffer.byteLength(JSON.stringify(doc)) / 4) * 4, ' '));
  const b = Buffer.alloc(28 + json.length + 36); b.writeUInt32LE(0x46546c67, 0); b.writeUInt32LE(2, 4); b.writeUInt32LE(b.length, 8); b.writeUInt32LE(json.length, 12); b.writeUInt32LE(0x4e4f534a, 16); json.copy(b, 20); b.writeUInt32LE(36, 20 + json.length); b.writeUInt32LE(0x004e4942, 24 + json.length); b.writeFloatLE(1, 28 + json.length + 12); b.writeFloatLE(1, 28 + json.length + 28); return b;
}
async function setup(t, options = {}) {
  const dataDir = options.dataDir ?? await mkdtemp(path.join(tmpdir(), 'healsphere-creation-'));
  const s = await createApp({ key: 'fake-tripo', token, enabled: true, dataDir, pollMs: 1, reviewMode: 'manual', ...options });
  const server = s.app.listen(0, '127.0.0.1'); await new Promise(r => server.once('listening', r));
  t.after(async () => { await s.waitForWorker(); await new Promise(r => server.close(r)); });
  const request = (route, body) => fetch(`http://127.0.0.1:${server.address().port}${route}`, { method: body ? 'POST' : 'GET', headers: { 'Content-Type': 'application/json', 'X-Backend-Token': token }, ...(body ? { body: JSON.stringify(body) } : {}) });
  const idea = async (text = 'I want a little sleeping fox.', extra = {}) => (await request('/api/conversation', { idea: text, ...extra })).json();
  return { ...s, request, idea, dataDir };
}
const confirm = proposal => ({ proposal_id: proposal.proposal_id, confirm: 'CREATE_ONE_MODEL', mode: 'live' });

test('valid ideas remain proposals until explicit confirmation; empty and unsafe input fail', async t => {
  const s = await setup(t, { fetchFn: () => assert.fail('proposal must not call Tripo') });
  const p = await s.idea(); assert.equal(p.accepted, true); assert.equal(p.source, 'offline'); assert.match(p.prompt, /sleeping fox/);
  assert.equal((await s.request('/api/conversation', { idea: '' })).status, 400);
  assert.equal((await s.request('/api/conversation', { idea: 'a bomb' })).status, 400);
  assert.equal((await s.request('/api/creations', { proposal_id: p.proposal_id })).status, 400);
});
test('one friendly clarification retains context; offline companion is labeled', async t => {
  const s = await setup(t); const question = await s.idea('something'); assert.equal(question.needsClarification, true); assert.ok(question.question);
  const answer = await s.idea('a sleeping fox', { clarification_id: question.clarification_id }); assert.equal(answer.accepted, true); assert.match(answer.displayName, /fox/); assert.equal(answer.source, 'offline');
  assert.throws(() => validateInterpretation({ ...offlineInterpret('fox toy'), accepted: 'yes' }));
});
test('OpenAI structured Responses request is moderated and server-validated, without Tripo submission', async t => {
  let calls = 0;
  const s = await setup(t, { openaiKey: 'fake-openai-secret', fetchFn: async (url, opts) => {
    assert.ok(String(url).startsWith('https://api.openai.com/v1/')); calls++;
    if (String(url).endsWith('/moderations')) return Response.json({ results: [{ flagged: false }] });
    const body = JSON.parse(opts.body); assert.equal(body.text.format.strict, true); assert.equal(body.store, false); assert.equal(body.text.format.schema.additionalProperties, false);
    return Response.json({ status: 'completed', output: [{ content: [{ type: 'output_text', text: JSON.stringify(offlineInterpret('little sleeping fox')) }] }] });
  } });
  const p = await s.idea(); assert.equal(p.source, 'openai'); assert.equal(p.accepted, true); assert.equal(calls, 3); assert.ok(!JSON.stringify(p).includes('secret'));
});
test('OpenAI failure and invalid structured output fail closed with sanitized errors', async t => {
  for (const invalid of [false, true]) {
    const s = await setup(t, { openaiKey: 'fake-key', fetchFn: async url => {
      if (String(url).endsWith('/moderations')) return Response.json({ results: [{ flagged: false }] });
      if (!invalid) throw new Error('sensitive raw upstream credentials');
      return Response.json({ status: 'completed', output: [{ content: [{ type: 'output_text', text: '{"accepted":true}' }] }] });
    } });
    const response = await s.request('/api/conversation', { idea: 'sleeping fox' }); assert.equal(response.status, 400); assert.ok(!(await response.text()).includes('sensitive'));
  }
});
test('dynamic task submits once, polls pending, validates GLB, gates review, and survives restart', async t => {
  let posts = 0, polls = 0;
  const s = await setup(t, { fetchFn: async (url, opts) => {
    url = String(url);
    if (url.endsWith('/account/balance')) return ok({ balance: 50, frozen: 0 });
    if (url.endsWith('/generation/text-to-model')) { posts++; const body = JSON.parse(opts.body); assert.match(body.prompt, /sleeping fox/); assert.equal(body.model, 'P1-20260311'); return ok({ task_id: 'dynamic-task' }); }
    if (url.endsWith('/tasks/dynamic-task')) return ok({ task_id: 'dynamic-task', status: ++polls < 2 ? 'running' : 'success', progress: 25, output: { model_url: 'https://cdn.tripo3d.ai/private.glb?signature=secret' } });
    assert.equal(opts.headers, undefined); return new Response(glb());
  } });
  const p = await s.idea(); const first = await s.request('/api/creations', confirm(p)); assert.equal(first.status, 202); const job = await first.json();
  await s.waitForWorker(); let state = await (await s.request('/api/creations/' + job.id)).json(); assert.equal(state.status, 'awaiting_review');
  assert.ok(!JSON.stringify(state).includes('signature')); assert.equal((await s.request('/api/creations/' + job.id + '/model')).status, 409);
  await s.request('/api/creations/' + job.id + '/review', { decision: 'APPROVE' }); assert.deepEqual(Buffer.from(await (await s.request('/api/creations/' + job.id + '/model')).arrayBuffer()), glb());
  await s.request('/api/creations', confirm(p)); assert.equal(posts, 1); assert.equal(polls, 2);
  const restarted = await setup(t, { dataDir: s.dataDir, fetchFn: () => assert.fail('no new paid request on restart') });
  assert.equal((await (await restarted.request('/api/creations', confirm(p))).json()).id, job.id);
});
test('generation failure, timeout and invalid GLB recover to a non-ready state', async t => {
  for (const kind of ['failure', 'timeout', 'invalid_glb']) {
    const s = await setup(t, { taskTimeoutMs: kind === 'timeout' ? 15 : 1000, fetchFn: async url => {
      url = String(url); if (url.endsWith('/account/balance')) return ok({ balance: 10, frozen: 0 });
      if (url.endsWith('/generation/text-to-model')) return ok({ task_id: 'task' });
      if (url.endsWith('/tasks/task')) return ok({ task_id: 'task', status: kind === 'failure' ? 'failed' : kind === 'timeout' ? 'running' : 'success', output: { model_url: 'https://cdn.tripo3d.ai/test.glb' } });
      return new Response(Buffer.from('invalid'));
    } });
    const result = await (await s.request('/api/creations', confirm(await s.idea()))).json(); await s.waitForWorker();
    assert.equal((await (await s.request('/api/creations/' + result.id)).json()).status, 'failed');
  }
});
test('cancellation during submission prevents a worker and never duplicates the paid task', async t => {
  let release, submitted = false, posts = 0;
  const s = await setup(t, { fetchFn: async url => {
    if (String(url).endsWith('/account/balance')) return ok({ balance: 10, frozen: 0 });
    if (String(url).endsWith('/generation/text-to-model')) { posts++; submitted = true; return new Promise(resolve => { release = () => resolve(ok({ task_id: 'cancelled-remote' })); }); }
    assert.fail('cancelled submission must not poll');
  } });
  const p = await s.idea(); const pending = s.request('/api/creations', confirm(p)); while (!submitted) await new Promise(r => setTimeout(r, 1));
  await s.request('/api/proposals/' + p.proposal_id + '/cancel', {}); release(); const job = await (await pending).json(); assert.equal(job.status, 'cancelled');
  await s.request('/api/creations', confirm(p)); assert.equal(posts, 1);
});
test('deterministic demo and repeated sessions use local reviewed assets without upstream calls', async t => {
  const dir = await mkdtemp(path.join(tmpdir(), 'healsphere-demo-')); const demo = path.join(dir, 'demo.glb'); await writeFile(demo, glb());
  const s = await setup(t, { enabled: false, demoModelPath: demo, demoDelayMs: 1, fetchFn: () => assert.fail('demo is offline') });
  const ids = [];
  for (const idea of ['sleeping fox', 'little dragon']) {
    const p = await s.idea(idea); const result = await s.request('/api/creations', { ...confirm(p), mode: 'demo' }); assert.equal(result.status, 202); const job = await result.json(); ids.push(job.id); await s.waitForWorker();
    const state = await (await s.request('/api/creations/' + job.id)).json(); assert.equal(state.status, 'ready'); assert.equal(state.source, 'demo'); assert.match(state.displayName, /Prepared Orange Cat/);
    assert.equal((await s.request('/api/creations/' + job.id + '/model')).status, 200);
  }
  assert.notEqual(ids[0], ids[1]);
  // A stopped server can leave a demo journal pending without a provider task_id.
  const journal = path.join(s.dataDir, 'creations', ids[1] + '.json');
  const pending = JSON.parse(await readFile(journal, 'utf8')); pending.status = 'generating';
  await writeFile(journal, JSON.stringify(pending));
  const restarted = await setup(t, { dataDir: s.dataDir, enabled: false, demoModelPath: demo, demoDelayMs: 1, fetchFn: () => assert.fail('resumed demo must stay offline') });
  await restarted.waitForWorker();
  assert.equal((await (await restarted.request('/api/creations/' + ids[1])).json()).status, 'ready');
});

test('automatic visual review approves, rejects, or leaves the asset gated on review failure', async t => {
  for (const outcome of ['approve', 'reject', 'unavailable']) {
    const s = await setup(t, { openaiKey: 'fake-review-key', reviewMode: 'openai', fetchFn: async (address, options) => {
      const url = String(address);
      if (url.endsWith('/moderations')) return Response.json({ results: [{ flagged: false }] });
      if (url.endsWith('/responses')) {
        const body = JSON.parse(options.body), reviewing = body.text.format.name === 'asset_review';
        if (reviewing && outcome === 'unavailable') throw new Error('private upstream review failure');
        return Response.json({ status: 'completed', output: [{ content: [{ type: 'output_text', text: JSON.stringify(reviewing ? { approved: outcome === 'approve' } : offlineInterpret('sleeping fox')) }] }] });
      }
      if (url.endsWith('/account/balance')) return ok({ balance: 10, frozen: 0 });
      if (url.endsWith('/generation/text-to-model')) return ok({ task_id: 'review-task' });
      if (url.endsWith('/tasks/review-task')) return ok({ task_id: 'review-task', status: 'success', output: { model_url: 'https://cdn.tripo3d.ai/test.glb', rendered_image_url: 'https://cdn.tripo3d.ai/test.png' } });
      if (url.endsWith('.png')) return new Response(Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jxZ8AAAAASUVORK5CYII=', 'base64'), { headers: { 'Content-Type': 'image/png' } });
      return new Response(glb());
    } });
    const job = await (await s.request('/api/creations', confirm(await s.idea()))).json(); await s.waitForWorker();
    const state = await (await s.request('/api/creations/' + job.id)).json();
    assert.equal(state.status, outcome === 'approve' ? 'ready' : outcome === 'reject' ? 'rejected' : 'awaiting_review');
    assert.equal((await s.request('/api/creations/' + job.id + '/model')).status, outcome === 'approve' ? 200 : 409);
    assert.ok(!JSON.stringify(state).includes('private upstream'));
    if (outcome === 'reject') {
      const route = '/api/creations/' + job.id + '/review';
      assert.equal((await s.request(route, { decision: 'APPROVE' })).status, 409, 'Rejection cannot be silently bypassed');
      assert.equal((await s.request(route, { decision: 'APPROVE', confirm: 'APPROVE_REVIEWED_EXISTING_ASSET' })).status, 409, 'Review note required');
      const recovered = await s.request(route, { decision: 'APPROVE', confirm: 'APPROVE_REVIEWED_EXISTING_ASSET', note: 'Locally inspected existing preview and validated model; safe matching test asset.' });
      assert.equal(recovered.status, 200); assert.equal((await recovered.json()).status, 'ready');
      assert.equal((await s.request('/api/creations/' + job.id + '/model')).status, 200);
      const journal = JSON.parse(await readFile(path.join(s.dataDir, 'creations', job.id + '.json'), 'utf8'));
      assert.equal(journal.review_history[0].previousStatus, 'rejected');
      assert.equal(journal.review_history[0].previousReview, 'openai_visual_review');
      assert.equal(journal.review_history[0].previousError, 'MODEL_REVIEW_REJECTED');
    }
  }
});

test('owner-disabled visual review serves validated models immediately, persists reuse, and still rejects invalid GLBs', async t => {
  for (const valid of [true, false]) {
    let posts = 0, previews = 0;
    const s = await setup(t, { reviewMode: 'none', fetchFn: async address => {
      const url = String(address);
      if (url.endsWith('/account/balance')) return ok({ balance: 100, frozen: 0 });
      if (url.endsWith('/generation/text-to-model')) { posts++; return ok({ task_id: 'no-review-task' }); }
      if (url.endsWith('/tasks/no-review-task')) return ok({ task_id: 'no-review-task', status: 'success', output: { model_url: 'https://cdn.tripo3d.ai/model.glb', rendered_image_url: 'https://cdn.tripo3d.ai/preview.png' } });
      if (url.endsWith('.png') || url.includes('openai.com')) { previews++; assert.fail('Visual review must be skipped'); }
      return new Response(valid ? glb() : Buffer.from('invalid'));
    } });
    const job = await (await s.request('/api/creations', confirm(await s.idea()))).json(); await s.waitForWorker();
    const state = await (await s.request('/api/creations/' + job.id)).json();
    assert.equal(state.status, valid ? 'ready' : 'failed'); assert.equal(previews, 0); assert.equal(posts, 1);
    assert.equal((await s.request('/api/creations/' + job.id + '/model')).status, valid ? 200 : 409);
    if (valid) {
      assert.equal(state.review, 'skipped_by_owner');
      const restarted = await setup(t, { dataDir: s.dataDir, reviewMode: 'none', enabled: false, fetchFn: () => assert.fail('Saved reuse must not call a provider') });
      const p = await restarted.idea(); assert.equal(p.saved_creation_id, state.creation_id);
      const reused = await (await restarted.request('/api/creations', { proposal_id: p.proposal_id, mode: 'reuse' })).json();
      assert.equal(reused.status, 'ready'); assert.equal(reused.review, 'skipped_by_owner');
      const gated = await setup(t, { dataDir: s.dataDir, reviewMode: 'manual', enabled: false, fetchFn: () => assert.fail('No provider call') });
      assert.equal((await gated.idea()).saved_creation_id, '', 'Reenabling review hides previously skipped assets');
    }
  }
});

test('disabling visual review releases an existing waiting model without resubmission', async t => {
  let posts = 0;
  const s = await setup(t, { fetchFn: async address => {
    const url = String(address);
    if (url.endsWith('/account/balance')) return ok({ balance: 100, frozen: 0 });
    if (url.endsWith('/generation/text-to-model')) { posts++; return ok({ task_id: 'waiting-task' }); }
    if (url.endsWith('/tasks/waiting-task')) return ok({ task_id: 'waiting-task', status: 'success', output: { model_url: 'https://cdn.tripo3d.ai/model.glb' } });
    return new Response(glb());
  } });
  const job = await (await s.request('/api/creations', confirm(await s.idea()))).json(); await s.waitForWorker();
  assert.equal((await (await s.request('/api/creations/' + job.id)).json()).status, 'awaiting_review');
  const restarted = await setup(t, { dataDir: s.dataDir, reviewMode: 'none', fetchFn: () => assert.fail('Release uses only saved bytes') });
  const state = await (await restarted.request('/api/creations/' + job.id)).json();
  assert.equal(state.status, 'ready'); assert.equal(state.review, 'skipped_by_owner'); assert.equal(posts, 1);
  assert.equal((await restarted.request('/api/creations/' + job.id + '/model')).status, 200);
});
