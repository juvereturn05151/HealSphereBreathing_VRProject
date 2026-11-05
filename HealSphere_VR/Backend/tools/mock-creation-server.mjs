// Local integration fixture only. Every upstream call is intercepted; this file never loads .env.
import { readFile, mkdtemp, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { createApp } from '../src/app.js';
import { offlineInterpret } from '../src/conversation.js';
import { validateGlb } from '../src/glb.js';
const token = 'healsphere-local-mock-only-0123456789';
const original = await readFile(new URL('../../Assets/StreamingAssets/healsphere-cat-demo.glb', import.meta.url));
// Reduce ONLY an in-memory copy's index count to exercise the production live budget.
// This deliberately incomplete test mesh is not an asset delivered to players or a generated fox.
const jsonLength = original.readUInt32LE(12), doc = JSON.parse(original.subarray(20, 20 + jsonLength));
const binary = Buffer.from(original.subarray(28 + jsonLength));
for (const mesh of doc.meshes) for (const primitive of mesh.primitives) {
  const a = doc.accessors[primitive.indices], view = doc.bufferViews[a.bufferView];
  const width = a.componentType === 5123 ? 2 : 4, offset = (view.byteOffset ?? 0) + (a.byteOffset ?? 0);
  const source = Buffer.from(binary); const triangles = Math.floor(a.count / 3), keep = Math.min(5000, triangles);
  let min = Infinity, max = -Infinity;
  for (let t = 0; t < keep; t++) for (let v = 0; v < 3; v++) {
    const from = offset + (Math.floor(t * triangles / keep) * 3 + v) * width;
    const value = width === 2 ? source.readUInt16LE(from) : source.readUInt32LE(from);
    if (width === 2) binary.writeUInt16LE(value, offset + (t * 3 + v) * width); else binary.writeUInt32LE(value, offset + (t * 3 + v) * width);
    min = Math.min(min, value); max = Math.max(max, value);
  }
  a.count = keep * 3; a.min = [min]; a.max = [max];
}
const json = Buffer.from(JSON.stringify(doc).padEnd(Math.ceil(Buffer.byteLength(JSON.stringify(doc)) / 4) * 4, ' '));
const glb = Buffer.alloc(28 + json.length + binary.length);
glb.writeUInt32LE(0x46546c67, 0); glb.writeUInt32LE(2, 4); glb.writeUInt32LE(glb.length, 8);
glb.writeUInt32LE(json.length, 12); glb.writeUInt32LE(0x4e4f534a, 16); json.copy(glb, 20);
glb.writeUInt32LE(binary.length, 20 + json.length); glb.writeUInt32LE(0x004e4942, 24 + json.length); binary.copy(glb, 28 + json.length);
await validateGlb(glb);
const stats = { submissions: 0, polls: 0, conversations: 0, reviews: 0, downloads: 0 };
const ok = data => Response.json({ code: 0, data });
const dataDir = await mkdtemp(path.join(tmpdir(), 'healsphere-unity-mock-'));
const { app } = await createApp({ key: 'mock-tripo-only', openaiKey: 'mock-openai-only', token, enabled: true, dataDir, pollMs: 250, reviewMode: 'openai',
  fetchFn: async (address, options) => {
    const url = String(address);
    if (url === 'https://api.openai.com/v1/moderations') return Response.json({ results: [{ flagged: false }] });
    if (url === 'https://api.openai.com/v1/responses') {
      const body = JSON.parse(options.body), review = body.text.format.name === 'asset_review';
      if (review) stats.reviews++; else stats.conversations++;
      const result = review ? { approved: true } : offlineInterpret(body.input);
      return Response.json({ status: 'completed', output: [{ content: [{ type: 'output_text', text: JSON.stringify(result) }] }] });
    }
    if (url.endsWith('/account/balance')) return ok({ balance: 100, frozen: 0 });
    if (url.endsWith('/generation/text-to-model')) { stats.submissions++; return ok({ task_id: 'mock-' + stats.submissions }); }
    if (/\/tasks\/mock-\d+$/.test(url)) {
      stats.polls++; return ok({ task_id: url.split('/').pop(), status: stats.polls % 2 ? 'running' : 'success', progress: 30,
        output: { model_url: 'https://cdn.tripo3d.ai/mock.glb', rendered_image_url: 'https://cdn.tripo3d.ai/mock.png' } });
    }
    if (url === 'https://cdn.tripo3d.ai/mock.glb') { stats.downloads++; return new Response(glb); }
    if (url === 'https://cdn.tripo3d.ai/mock.png') return new Response(Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jxZ8AAAAASUVORK5CYII=', 'base64'), { headers: { 'Content-Type': 'image/png' } });
    throw new Error('Unexpected mock upstream request; network is never used.');
  }
});
app.get('/mock-report', (req, res) => res.json(stats));
app.listen(3317, '127.0.0.1', async () => {
  await writeFile(new URL('../../Library/HealSphereMockReady.txt', import.meta.url), 'MOCK ONLY: localhost:3317; all provider calls intercepted.');
  console.log('Mock integration server ready on localhost:3317. No provider network or paid calls.');
});
