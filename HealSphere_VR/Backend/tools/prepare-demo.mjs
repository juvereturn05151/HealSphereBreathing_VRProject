// Correct glTFast's rounded POSITION bounds on the locally exported demo only.
// Geometry, indices, textures and materials are unchanged. Live downloads are never repaired.
import { readFile, writeFile, rename } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { validateGlb } from '../src/glb.js';
const file = fileURLToPath(new URL('../../Assets/StreamingAssets/healsphere-cat-demo.glb', import.meta.url));
const original = await readFile(file), jsonSize = original.readUInt32LE(12);
const doc = JSON.parse(original.subarray(20, 20 + jsonSize).toString('utf8'));
const bin = original.subarray(28 + jsonSize);
for (const id of new Set(doc.meshes.flatMap(m => m.primitives.map(p => p.attributes.POSITION)))) {
  const a = doc.accessors[id], view = doc.bufferViews[a.bufferView];
  if (a.type !== 'VEC3' || a.componentType !== 5126 || a.sparse || view.buffer !== 0) throw new Error('Unsupported demo POSITION layout.');
  const min = [Infinity, Infinity, Infinity], max = [-Infinity, -Infinity, -Infinity];
  for (let i = 0; i < a.count; i++) for (let c = 0; c < 3; c++) {
    const value = bin.readFloatLE((view.byteOffset ?? 0) + (a.byteOffset ?? 0) + i * (view.byteStride ?? 12) + c * 4);
    if (!Number.isFinite(value)) throw new Error('Invalid demo coordinate.');
    min[c] = Math.min(min[c], value); max[c] = Math.max(max[c], value);
  }
  a.min = min; a.max = max;
}
const text = JSON.stringify(doc), json = Buffer.from(text.padEnd(Math.ceil(Buffer.byteLength(text) / 4) * 4, ' '));
const bytes = Buffer.alloc(28 + json.length + bin.length); bytes.writeUInt32LE(0x46546c67, 0); bytes.writeUInt32LE(2, 4); bytes.writeUInt32LE(bytes.length, 8); bytes.writeUInt32LE(json.length, 12); bytes.writeUInt32LE(0x4e4f534a, 16); json.copy(bytes, 20); bytes.writeUInt32LE(bin.length, 20 + json.length); bytes.writeUInt32LE(0x004e4942, 24 + json.length); bin.copy(bytes, 28 + json.length);
const metadata = await validateGlb(bytes, { maxTriangles: 60000 });
await writeFile(file + '.tmp', bytes); await rename(file + '.tmp', file);
console.log('Reviewed local demo validated:', metadata);
