// Read-only checks against the protected reuse-verification service. No generation or OpenAI calls.
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { configuration } from '../src/config.js';
import { validateGlb } from '../src/glb.js';
const config = configuration();
const base = 'http://127.0.0.1:3337';
async function request(route, body) {
  const response = await fetch(base + route, { headers: { 'X-Backend-Token': config.token, 'Content-Type': 'application/json' },
    method: body ? 'POST' : 'GET', ...(body ? { body: JSON.stringify(body) } : {}), signal: AbortSignal.timeout(30000) });
  assert.equal(response.status, 200); return response;
}
const capability = await (await request('/api/capabilities')).json();
assert.equal(capability.liveGenerationEnabled, false); assert.equal(capability.generationBudget.used, 1);
let creation;
for (const idea of ['I want a cute sleeping fox.', 'A little fox sleeping peacefully.', 'Can you make a cute fox taking a nap?']) {
  const result = await (await request('/api/library/search', { idea })).json();
  assert.equal(result.creations[0].id, '539a8902-999b-4494-98d6-0a8add25e4a0'); creation = result.creations[0];
}
for (const idea of ['a standing fox', 'a blue fox with wings', 'a small purple baby dragon', 'a curled sleeping fox']) {
  assert.equal((await (await request('/api/library/search', { idea })).json()).creations.length, 0);
}
const bytes = Buffer.from(await (await request(creation.modelEndpoint)).arrayBuffer());
assert.equal(createHash('sha256').update(bytes).digest('hex'), creation.contentHash);
assert.equal((await validateGlb(bytes)).triangles, 4942);
assert.ok((await request('/api/library/' + creation.id + '/preview')).headers.get('content-type').startsWith('image/'));
console.log('PASS: persistent real fox, exact / peaceful / taking-a-nap matches, standing / winged blue / dragon / curled exclusions, authenticated GLB hash and geometry, approved preview, generation disabled, one durable paid submission.');
