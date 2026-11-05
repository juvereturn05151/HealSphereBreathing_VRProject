import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, readdir, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { randomUUID } from 'node:crypto';
import { openLibrary } from '../src/library.js';
import { requirements, satisfies } from '../src/matching.js';
import { createApp } from '../src/app.js';

function fixture() {
  const doc = { asset: { version: '2.0' }, scene: 0, scenes: [{ nodes: [0] }], nodes: [{ mesh: 0 }], meshes: [{ primitives: [{ attributes: { POSITION: 0 } }] }], accessors: [{ bufferView: 0, componentType: 5126, count: 3, type: 'VEC3', min: [0, 0, 0], max: [1, 1, 0] }], bufferViews: [{ buffer: 0, byteLength: 36 }], buffers: [{ byteLength: 36 }] };
  const raw = JSON.stringify(doc), json = Buffer.from(raw.padEnd(Math.ceil(Buffer.byteLength(raw) / 4) * 4, ' '));
  const b = Buffer.alloc(28 + json.length + 36); b.writeUInt32LE(0x46546c67); b.writeUInt32LE(2, 4); b.writeUInt32LE(b.length, 8); b.writeUInt32LE(json.length, 12); b.writeUInt32LE(0x4e4f534a, 16); json.copy(b, 20); b.writeUInt32LE(36, 20 + json.length); b.writeUInt32LE(0x004e4942, 24 + json.length); b.writeFloatLE(1, 28 + json.length + 12); b.writeFloatLE(1, 28 + json.length + 28); return b;
}
const fox = { owner: 'owner-a', bytes: fixture(), originalRequest: 'I want a cute sleeping fox.', prompt: 'Orange little fox curled up and sleeping, soft pastel stylized low-poly model', displayName: 'Sleeping fox', provider: 'tripo', taskId: 'mock-task', source: 'AI-generated', review: 'operator_review' };
async function temp() { return mkdtemp(path.join(tmpdir(), 'healsphere-library-')); }

test('two-stage matching handles exact/equivalent wording and rejects important differences or unknown requirements', () => {
  const asset = requirements(fox.prompt);
  for (const request of ['I want a cute sleeping fox.', 'Make a little fox curled up and sleeping.', 'A little fox sleeping peacefully.', 'Can you make a cute fox taking a nap?']) assert.equal(satisfies(requirements(request), asset), true, request);
  for (const request of ['a blue fox with wings', 'a small purple baby dragon', 'a standing fox', 'a fox with a hat', 'a realistic fox', 'a fox with cybernetic eyes', 'a fox without a tail']) assert.equal(satisfies(requirements(request), asset), false, request);
  assert.equal(satisfies(requirements('a fox'), requirements('a fox with wings')), false);
  assert.equal(satisfies(requirements('a curled sleeping fox'), requirements('an orange sleeping fox')), false, 'Never infer an unverified curled pose');
  assert.equal(satisfies(requirements('I want a gorilla'), requirements('One complete-body gorilla, low-poly pastel friendly standing pose')), true);
  assert.equal(satisfies(requirements('a sleeping gorilla'), requirements('a standing gorilla')), false);
});
test('approved library persists, deduplicates files, retrieves validated bytes and excludes rejected/unreviewed models', async () => {
  const dataDir = await temp(); let library = await openLibrary({ dataDir });
  const first = await library.register(fox); const again = await library.register({ ...fox, originalRequest: 'another description' });
  assert.equal(first.id, again.id); assert.equal((await readdir(path.join(dataDir, 'library/assets'))).filter(x => x.endsWith('.glb')).length, 1);
  assert.equal(library.search('a little fox sleeping peacefully', fox.owner)[0].id, first.id);
  assert.deepEqual(await library.model(first.id, fox.owner), fixture());
  assert.equal(library.search('a purple baby dragon', fox.owner).length, 0);
  await assert.rejects(library.register({ ...fox, review: 'rejected' }), /LIBRARY_REVIEW_REQUIRED/);
  await assert.rejects(library.register({ ...fox, bytes: Buffer.from('bad') }), /MODEL_REJECTED/);
  library.close(); library = await openLibrary({ dataDir });
  assert.equal(library.search('cute sleeping fox', fox.owner)[0].id, first.id); assert.deepEqual(await library.model(first.id, fox.owner), fixture()); library.close();
});
test('private assets are isolated; explicitly published shared metadata omits original player requests', async () => {
  const dataDir = await temp(); const library = await openLibrary({ dataDir, sharedCollection: true }); const asset = await library.register(fox);
  assert.equal(library.get(asset.id, 'unrelated-user'), null); await assert.rejects(library.model(asset.id, 'unrelated-user'), /CREATION_NOT_FOUND/);
  library.publish(asset.id, fox.owner); const shared = library.get(asset.id, 'unrelated-user');
  assert.equal(shared.source, 'AI-generated'); assert.equal(shared.originalRequest, undefined); assert.equal(shared.normalizedPrompt, undefined); assert.equal(shared.attributes.unknown, undefined);
  assert.ok(!JSON.stringify(shared).includes(dataDir)); library.close();
});
test('content tampering and unsafe IDs are rejected; retirement prevents subsequent reuse', async () => {
  const dataDir = await temp(); const library = await openLibrary({ dataDir }); const asset = await library.register(fox);
  assert.equal(library.get('../creations.sqlite', fox.owner), null);
  await writeFile(path.join(dataDir, 'library/assets', asset.contentHash + '.glb'), Buffer.from('tampered'));
  await assert.rejects(library.model(asset.id, fox.owner), /MODEL_REJECTED/);
  library.retire(asset.id, fox.owner); assert.equal(library.search('sleeping fox', fox.owner).length, 0); library.close();
});
test('submission reservations and per-session/lifetime budgets survive a backend restart', async () => {
  const dataDir = await temp(); let library = await openLibrary({ dataDir }); const sessionId = library.session(fox.owner), jobId = randomUUID();
  const reservation = { owner: fox.owner, key: 'durable-test-key', requestKey: 'fox', sessionId, jobId, perSession: 1, lifetime: 1 };
  assert.equal(library.reserve(reservation), jobId); library.close(); library = await openLibrary({ dataDir });
  assert.equal(library.validSession(sessionId, fox.owner), true); assert.equal(library.validSession(sessionId, 'other'), false);
  assert.equal(library.reserve({ ...reservation, jobId: randomUUID() }), jobId);
  assert.throws(() => library.reserve({ ...reservation, key: 'new-key-2', requestKey: 'dragon', lifetime: 10 }), /SESSION_GENERATION_LIMIT/);
  assert.throws(() => library.reserve({ ...reservation, key: 'new-key-3', sessionId: library.session(fox.owner) }), /GENERATION_BUDGET_EXHAUSTED/); library.close();
});

test('explicit unlimited quotas allow new paid reservations while retaining history and duplicate protection', async () => {
  const { generationSettings } = await import('../src/limits.js');
  assert.equal(generationSettings({ maxLifetime: -1, perSession: -1 }).maxLifetime, -1);
  assert.throws(() => generationSettings({ maxLifetime: -2 }), /INVALID_GENERATION_LIMIT/);
  assert.throws(() => generationSettings({ perSession: -2 }), /INVALID_GENERATION_LIMIT/);
  const dataDir = await temp(); let library = await openLibrary({ dataDir });
  const sessionId = library.session(fox.owner);
  for (let i = 0; i < 3; i++) {
    const reservation = { owner: fox.owner, key: 'unlimited-key-' + i, requestKey: 'object-' + i, sessionId, jobId: randomUUID(), perSession: -1, lifetime: -1 };
    assert.equal(library.reserve(reservation), reservation.jobId);
    assert.equal(library.reserve({ ...reservation, jobId: randomUUID() }), reservation.jobId);
  }
  assert.equal(library.submissionCount(), 3); library.close(); library = await openLibrary({ dataDir });
  assert.equal(library.submissionCount(), 3);
  assert.throws(() => library.reserve({ owner: fox.owner, key: 'restored-quota', requestKey: 'different', sessionId, jobId: randomUUID(), perSession: 1, lifetime: 1 }), /SESSION_GENERATION_LIMIT/);
  library.close();
});

test('real HTTP routes reuse approved GLB after restart with zero additional provider calls and no paid confirmation', async t => {
  const dataDir = await temp(), token = 'library-tests-capability-0123456789-abcdef'; let posts = 0;
  const ok = data => Response.json({ code: 0, data });
  const first = await createApp({ key: 'fake-tripo', token, enabled: true, dataDir, pollMs: 1, reviewMode: 'manual', fetchFn: async url => {
    url = String(url); if (url.endsWith('/account/balance')) return ok({ balance: 100, frozen: 0 });
    if (url.endsWith('/generation/text-to-model')) { posts++; return ok({ task_id: 'single-mock-fox' }); }
    if (url.endsWith('/tasks/single-mock-fox')) return ok({ task_id: 'single-mock-fox', status: 'success', output: { model_url: 'https://cdn.tripo3d.ai/fox.glb' } });
    return new Response(fixture());
  } });
  async function serving(app) {
    const server = app.listen(0, '127.0.0.1'); await new Promise(r => server.once('listening', r)); t.after(() => new Promise(r => server.close(r)));
    return async (route, body) => fetch('http://127.0.0.1:' + server.address().port + route, { method: body ? 'POST' : 'GET', headers: { 'X-Backend-Token': token, 'Content-Type': 'application/json' }, ...(body ? { body: JSON.stringify(body) } : {}) });
  }
  const request = await serving(first.app); const session = await (await request('/api/sessions', {})).json();
  const p = await (await request('/api/conversation', { idea: 'I want a cute sleeping fox.' })).json(); assert.equal(p.saved_creation_id, '');
  const job = await (await request('/api/creations', { proposal_id: p.proposal_id, session_id: session.session_id, idempotency_key: p.proposal_id, mode: 'live', confirm: 'CREATE_ONE_MODEL' })).json();
  await first.waitForWorker(); assert.equal((await (await request('/api/library')).json()).creations.length, 0);
  await request('/api/creations/' + job.id + '/review', { decision: 'APPROVE' });
  const second = await createApp({ key: 'fake-tripo', token, enabled: false, dataDir, fetchFn: () => assert.fail('reuse must not access any generation provider') }); const reuseRequest = await serving(second.app);
  const idea = await (await reuseRequest('/api/conversation', { idea: 'A little fox sleeping peacefully.' })).json(); assert.ok(idea.saved_creation_id);
  const reused = await (await reuseRequest('/api/creations', { proposal_id: idea.proposal_id, mode: 'reuse' })).json(); assert.equal(reused.source, 'library'); assert.equal(reused.status, 'ready');
  assert.deepEqual(Buffer.from(await (await reuseRequest('/api/creations/' + reused.id + '/model')).arrayBuffer()), fixture());
  const dragon = await (await reuseRequest('/api/conversation', { idea: 'A small purple baby dragon.' })).json(); assert.equal(dragon.saved_creation_id, '');
  const nap = await (await reuseRequest('/api/conversation', { idea: 'Can you make a cute fox taking a nap?' })).json(); assert.equal(nap.saved_creation_id, idea.saved_creation_id);
  const standing = await (await reuseRequest('/api/conversation', { idea: 'A standing fox.' })).json(); assert.equal(standing.saved_creation_id, '');
  assert.equal((await reuseRequest('/api/creations', { proposal_id: standing.proposal_id, mode: 'live', confirm: 'CREATE_ONE_MODEL' })).status, 403);
  assert.equal((await reuseRequest('/api/creations', { proposal_id: dragon.proposal_id, mode: 'live' })).status, 400); assert.equal(posts, 1);
  first.close(); second.close();
});

test('rate, session, storage, provider and numeric limits fail closed without a paid POST', async t => {
  const { generationSettings } = await import('../src/limits.js');
  assert.throws(() => generationSettings({ maxLifetime: NaN }), /INVALID_GENERATION_LIMIT/);
  assert.throws(() => generationSettings({ allowedProviders: ['unknown'] }), /INVALID_ALLOWED_PROVIDERS/);
  for (const generationLimits of [{ perSession: 0 }, { allowedProviders: [] }, { maxLifetime: 0 }]) {
    const backend = await createApp({ key: 'fake', token: 'limits-capability-0123456789-abcdefgh', enabled: true, dataDir: await temp(), generationLimits,
      fetchFn: async url => { assert.ok(String(url).endsWith('/account/balance')); return Response.json({ code: 0, data: { balance: 100, frozen: 0 } }); } });
    const server = backend.app.listen(0, '127.0.0.1'); await new Promise(r => server.once('listening', r));
    const request = (route, body) => fetch('http://127.0.0.1:' + server.address().port + route, { method: 'POST', headers: { 'X-Backend-Token': 'limits-capability-0123456789-abcdefgh', 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
    const p = await (await request('/api/conversation', { idea: 'a sleeping fox' })).json();
    assert.ok((await request('/api/creations', { proposal_id: p.proposal_id, mode: 'live', confirm: 'CREATE_ONE_MODEL' })).status >= 400);
    await new Promise(r => server.close(r)); backend.close();
  }
  const dataDir = await temp(); const library = await openLibrary({ dataDir, maxStorageBytes: 100 });
  await assert.rejects(library.ensureSpace(101), /STORAGE_LIMIT_REACHED/); library.close();
  const backend = await createApp({ token: 'rate-capability-0123456789-abcdefgh', dataDir: await temp(), generationLimits: { requestsPerMinute: 1 }, fetchFn: () => assert.fail('No provider access') });
  const server = backend.app.listen(0, '127.0.0.1'); await new Promise(r => server.once('listening', r));
  const url = 'http://127.0.0.1:' + server.address().port + '/api/capabilities';
  const headers = { 'X-Backend-Token': 'rate-capability-0123456789-abcdefgh' };
  assert.equal((await fetch(url, { headers })).status, 200); assert.equal((await fetch(url, { headers })).status, 429);
  await new Promise(r => server.close(r)); backend.close();
});

test('equivalent in-progress requests in one session return the durable existing job without a second paid submission', async () => {
  let posts = 0, balanceChecks = 0;
  const token = 'inprogress-capability-0123456789-abcdef';
  const backend = await createApp({ key: 'fake', token, enabled: true, dataDir: await temp(), pollMs: 5, taskTimeoutMs: 1000,
    fetchFn: async url => {
      url = String(url);
      if (url.endsWith('/account/balance')) { balanceChecks++; return Response.json({ code: 0, data: { balance: 100, frozen: 0 } }); }
      if (url.endsWith('/generation/text-to-model')) { posts++; return Response.json({ code: 0, data: { task_id: 'pending-fox' } }); }
      return Response.json({ code: 0, data: { task_id: 'pending-fox', status: 'running', progress: 10 } });
    } });
  const server = backend.app.listen(0, '127.0.0.1'); await new Promise(r => server.once('listening', r));
  const post = async (route, body) => (await fetch('http://127.0.0.1:' + server.address().port + route, { method: 'POST', headers: { 'X-Backend-Token': token, 'Content-Type': 'application/json' }, body: JSON.stringify(body) })).json();
  try {
    const { session_id } = await post('/api/sessions', {});
    const first = await post('/api/conversation', { idea: 'a cute sleeping fox' });
    const job = await post('/api/creations', { proposal_id: first.proposal_id, session_id, mode: 'live', confirm: 'CREATE_ONE_MODEL' });
    const second = await post('/api/conversation', { idea: 'a little fox sleeping peacefully' });
    const recovered = await post('/api/creations', { proposal_id: second.proposal_id, session_id, mode: 'live', confirm: 'CREATE_ONE_MODEL' });
    assert.equal(recovered.id, job.id); assert.equal(posts, 1); assert.equal(balanceChecks, 1);
    await post('/api/creations/' + job.id + '/cancel', {}); await backend.waitForWorker();
  } finally { await new Promise(r => server.close(r)); backend.close(); }
});
