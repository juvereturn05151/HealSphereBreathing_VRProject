import validator from 'gltf-validator';
import { imageSize } from 'image-size';

export const MAX_BYTES = 32 * 1024 * 1024;

// Reject external resources and compressed assets that could expand beyond our budgets.
export async function validateGlb(bytes) {
  const reject = () => { throw new Error('MODEL_REJECTED'); };
  if (bytes.length < 28 || bytes.length > MAX_BYTES || bytes.readUInt32LE(0) !== 0x46546c67 ||
      bytes.readUInt32LE(4) !== 2 || bytes.readUInt32LE(8) !== bytes.length ||
      bytes.readUInt32LE(16) !== 0x4e4f534a) reject();
  const length = bytes.readUInt32LE(12);
  if (length > 1024 * 1024 || 20 + length > bytes.length) reject();
  const doc = JSON.parse(bytes.subarray(20, 20 + length).toString('utf8'));
  if (doc.asset?.version !== '2.0' || !doc.meshes?.length || !doc.scenes?.[doc.scene ?? 0] ||
      (doc.nodes?.length ?? 0) > 128 || (doc.materials?.length ?? 0) > 8 ||
      (doc.images?.length ?? 0) > 8 || doc.animations?.length || doc.skins?.length) reject();
  const allowed = new Set(['KHR_materials_unlit', 'KHR_texture_transform', 'KHR_mesh_quantization']);
  if ((doc.extensionsUsed ?? []).some(x => !allowed.has(x))) reject();
  if ((doc.buffers ?? []).some(x => x.uri) || (doc.images ?? []).some(x => x.uri)) reject();
  if (doc.buffers?.length !== 1 || doc.buffers[0].byteLength > MAX_BYTES) reject();
  const binHeader = 20 + length;
  if (binHeader + 8 > bytes.length || bytes.readUInt32LE(binHeader + 4) !== 0x004e4942 ||
      binHeader + 8 + bytes.readUInt32LE(binHeader) !== bytes.length) reject();
  let triangles = 0;
  let vertices = 0;
  const meshTriangles = [];
  for (const mesh of doc.meshes) {
    let count = 0;
    for (const p of mesh.primitives ?? []) {
      if ((p.mode ?? 4) !== 4 || p.targets || p.extensions) reject();
      const v = doc.accessors?.[p.attributes?.POSITION]?.count;
      const n = p.indices === undefined ? v : doc.accessors?.[p.indices]?.count;
      if (!Number.isSafeInteger(v) || !Number.isSafeInteger(n) || n % 3) reject();
      count += n / 3;
      vertices += v;
    }
    meshTriangles.push(count);
  }
  for (const node of doc.nodes ?? []) {
    if (node.extensions) reject();
    if (node.mesh !== undefined) triangles += meshTriangles[node.mesh] ?? Infinity;
  }
  if (!triangles || triangles > 20000 || vertices > 60000) reject();
  let pixels = 0;
  for (const image of doc.images ?? []) {
    if (!['image/png', 'image/jpeg'].includes(image.mimeType)) reject();
    const view = doc.bufferViews?.[image.bufferView];
    if (!view || view.buffer !== 0) reject();
    const start = binHeader + 8 + (view.byteOffset ?? 0);
    const end = start + view.byteLength;
    if (start < binHeader + 8 || end > bytes.length) reject();
    const size = imageSize(bytes.subarray(start, end));
    if (!Number.isSafeInteger(size.width) || !Number.isSafeInteger(size.height) ||
        size.width < 1 || size.height < 1 || size.width > 2048 || size.height > 2048) reject();
    pixels += size.width * size.height;
  }
  if (pixels > 16 * 1024 * 1024) reject();
  const report = await validator.validateBytes(new Uint8Array(bytes), { maxIssues: 16 });
  if (report.issues.numErrors) reject();
  return { triangles, materials: doc.materials?.length ?? 0, bytes: bytes.length };
}
