import { DatabaseSync } from 'node:sqlite';
import { createHash, randomUUID } from 'node:crypto';
import { mkdir, readFile, writeFile, rename, readdir, stat, unlink } from 'node:fs/promises';
import path from 'node:path';
import { validateGlb, MAX_BYTES } from './glb.js';
import { requirements, satisfies } from './matching.js';

export async function openLibrary({ dataDir, maxStorageBytes = 512 * 1024 * 1024, maxModelBytes = MAX_BYTES, sharedCollection = false, allowUnreviewed = false }) {
  const root = path.join(dataDir, 'library'); await mkdir(path.join(root, 'assets'), { recursive: true });
  const db = new DatabaseSync(path.join(root, 'creations.sqlite'));
  db.exec(`PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;
    CREATE TABLE IF NOT EXISTS creations (
      id TEXT PRIMARY KEY, owner_id TEXT NOT NULL, visibility TEXT NOT NULL, reuse_permission TEXT NOT NULL,
      original_request TEXT NOT NULL, normalized_prompt TEXT NOT NULL, display_name TEXT NOT NULL, category TEXT NOT NULL,
      attributes TEXT NOT NULL, provider TEXT NOT NULL, task_id TEXT NOT NULL, generated_at TEXT NOT NULL,
      content_hash TEXT NOT NULL, glb_path TEXT NOT NULL, preview_path TEXT, bytes INTEGER NOT NULL,
      validation TEXT NOT NULL, review TEXT NOT NULL, source TEXT NOT NULL, rights TEXT NOT NULL, retired INTEGER NOT NULL DEFAULT 0,
      UNIQUE(owner_id, content_hash));
    CREATE UNIQUE INDEX IF NOT EXISTS creation_task ON creations(owner_id, task_id) WHERE task_id != '';
    CREATE INDEX IF NOT EXISTS creation_category ON creations(category, retired, generated_at);
    CREATE TABLE IF NOT EXISTS submissions (idempotency_key TEXT PRIMARY KEY, owner_id TEXT NOT NULL, session_id TEXT NOT NULL,
      request_key TEXT NOT NULL, job_id TEXT NOT NULL UNIQUE, created_at TEXT NOT NULL);
    CREATE TABLE IF NOT EXISTS sessions (id TEXT PRIMARY KEY, owner_id TEXT NOT NULL, created_at TEXT NOT NULL);
  `);
  const validId = value => typeof value === 'string' && /^[a-f0-9-]{36}$/.test(value);
  function access(row, owner) { return row && !row.retired &&
    (row.review !== 'skipped_by_owner' || allowUnreviewed && row.owner_id === owner) &&
    (row.owner_id === owner || sharedCollection && row.visibility === 'shared' && row.reuse_permission === 'approved_shared_collection'); }
  function view(row, owner) {
    if (!access(row, owner)) return null;
    const attributes = JSON.parse(row.attributes); if (row.owner_id !== owner) delete attributes.unknown;
    const displayName = row.owner_id === owner ? row.display_name : [...(attributes.color ?? []), ...(attributes.pose ?? []), row.category].join(' ');
    const result = { id: row.id, displayName, category: row.category, attributes,
      provider: row.provider, task_id: row.task_id, generatedAt: row.generated_at, contentHash: row.content_hash, bytes: row.bytes,
      validation: JSON.parse(row.validation), review: row.review, source: row.source, visibility: row.visibility,
      reusePermission: row.reuse_permission, modelEndpoint: '/api/library/' + row.id + '/model', hasPreview: !!row.preview_path, rights: row.rights };
    if (row.owner_id === owner) { result.originalRequest = row.original_request; result.normalizedPrompt = row.normalized_prompt; }
    return result;
  }
  const rowFor = id => validId(id) ? db.prepare('SELECT * FROM creations WHERE id=?').get(id) : null;
  async function usage() {
    let bytes = 0;
    async function walk(dir) { for (const entry of await readdir(dir, { withFileTypes: true })) { if (entry.isSymbolicLink()) throw new Error('STORAGE_SYMLINK_REJECTED'); const file = path.join(dir, entry.name); if (entry.isDirectory()) await walk(file); else bytes += (await stat(file)).size; } }
    await walk(dataDir); return bytes;
  }
  async function ensureSpace(reserve) { if (await usage() + reserve > maxStorageBytes) throw new Error('STORAGE_LIMIT_REACHED'); }
  async function atomic(file, bytes) {
    const temp = file + '.' + randomUUID() + '.tmp';
    try { await writeFile(temp, bytes, { flag: 'wx', mode: 0o600 }); await rename(temp, file); }
    finally { await unlink(temp).catch(() => {}); }
  }
  async function register({ owner, bytes, originalRequest, prompt, displayName, provider, taskId = '', source,
    review, verifiedAttributes, preview, previewExtension = 'png', rights = 'subject_to_provider_terms' }) {
    const reviewAccepted = ['operator_review', 'openai_visual_review', 'reviewed_local_demo'].includes(review) || allowUnreviewed && review === 'skipped_by_owner';
    if (!owner || !['AI-generated', 'prebuilt'].includes(source) || !reviewAccepted) throw new Error('LIBRARY_REVIEW_REQUIRED');
    if (!bytes || bytes.length > maxModelBytes) throw new Error('MODEL_REJECTED');
    const validation = await validateGlb(bytes, { maxTriangles: source === 'prebuilt' ? 60000 : 20000 });
    validation.status = 'validated';
    const features = verifiedAttributes ?? requirements(originalRequest + ' ' + prompt);
    const hash = createHash('sha256').update(bytes).digest('hex');
    const existing = db.prepare('SELECT * FROM creations WHERE owner_id=? AND content_hash=?').get(owner, hash);
    if (existing) {
      const prior = JSON.parse(existing.attributes);
      if (['category','color','pose','anatomy','accessories','style','material','texture','age','size'].some(k => features[k]?.some(v => !prior[k]?.includes(v)))) throw new Error('CONTENT_METADATA_CONFLICT');
      return view(existing, owner);
    }
    await ensureSpace(bytes.length + (preview?.length ?? 0) + 8192);
    const glbPath = 'assets/' + hash + '.glb';
    try { await stat(path.join(root, glbPath)); } catch (e) { if (e.code !== 'ENOENT') throw e; await atomic(path.join(root, glbPath), bytes); }
    let previewPath = null;
    if (preview && preview.length <= 4 * 1024 * 1024 && ['png', 'jpg'].includes(previewExtension)) {
      previewPath = 'assets/' + hash + '.' + previewExtension; await atomic(path.join(root, previewPath), preview);
    }
    const id = randomUUID();
    db.prepare('INSERT INTO creations (id,owner_id,visibility,reuse_permission,original_request,normalized_prompt,display_name,category,attributes,provider,task_id,generated_at,content_hash,glb_path,preview_path,bytes,validation,review,source,rights) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)')
      .run(id, owner, 'private', 'owner_only', originalRequest, prompt, displayName, features.category.length === 1 ? features.category[0] : 'other', JSON.stringify(features), provider, taskId,
        new Date().toISOString(), hash, glbPath, previewPath, bytes.length, JSON.stringify(validation), review, source, rights);
    return view(rowFor(id), owner);
  }
  function search(text, owner) {
    const required = requirements(text); if (!required.conservative || required.category.length !== 1) return [];
    // Stage 1: bounded, indexed-by-category candidate retrieval; stage 2: explicit feature constraints.
    const rows = db.prepare('SELECT * FROM creations WHERE retired=0 AND category=? AND (owner_id=? OR (visibility=\'shared\' AND reuse_permission=\'approved_shared_collection\')) ORDER BY generated_at DESC LIMIT 64').all(required.category[0], owner);
    return rows.filter(row => access(row, owner) && satisfies(required, JSON.parse(row.attributes))).map(row => view(row, owner));
  }
  async function model(id, owner) {
    const row = rowFor(id); if (!access(row, owner)) throw new Error('CREATION_NOT_FOUND');
    if (!/^assets\/[a-f0-9]{64}\.glb$/.test(row.glb_path)) throw new Error('MODEL_REJECTED');
    const bytes = await readFile(path.join(root, row.glb_path));
    if (bytes.length > maxModelBytes || createHash('sha256').update(bytes).digest('hex') !== row.content_hash) throw new Error('MODEL_REJECTED');
    await validateGlb(bytes, { maxTriangles: row.source === 'prebuilt' ? 60000 : 20000 }); return bytes;
  }
  async function preview(id, owner) {
    const row = rowFor(id); if (!access(row, owner) || !row.preview_path) throw new Error('CREATION_NOT_FOUND');
    if (!new RegExp('^assets/' + row.content_hash + '\\.(png|jpg)$').test(row.preview_path)) throw new Error('MODEL_REJECTED');
    const bytes = await readFile(path.join(root, row.preview_path));
    if (bytes.length > 4 * 1024 * 1024) throw new Error('MODEL_REJECTED');
    return { bytes, mime: row.preview_path.endsWith('.png') ? 'image/png' : 'image/jpeg' };
  }
  function session(owner) {
    // Sessions do not contain player text and need not outlive old desktop runs. Paid ledger stays intact.
    db.prepare("DELETE FROM sessions WHERE created_at < datetime('now','-30 days')").run();
    if (db.prepare('SELECT COUNT(*) AS n FROM sessions').get().n >= 10000) throw new Error('SESSION_CAPACITY_REACHED');
    const id = randomUUID(); db.prepare('INSERT INTO sessions VALUES (?,?,?)').run(id, owner, new Date().toISOString()); return id;
  }
  function validSession(id, owner) { return validId(id) && !!db.prepare('SELECT id FROM sessions WHERE id=? AND owner_id=?').get(id, owner); }
  function reserve({ key, owner, sessionId, requestKey, jobId, perSession, lifetime }) {
    db.exec('BEGIN IMMEDIATE');
    try {
      const prior = db.prepare('SELECT * FROM submissions WHERE idempotency_key=?').get(key);
      if (prior) { if (prior.owner_id !== owner) throw new Error('IDEMPOTENCY_CONFLICT'); db.exec('COMMIT'); return prior.job_id; }
      const active = db.prepare('SELECT job_id FROM submissions WHERE owner_id=? AND session_id=? AND request_key=?').get(owner, sessionId, requestKey);
      if (active) { db.exec('COMMIT'); return active.job_id; }
      if (perSession !== -1 && db.prepare('SELECT COUNT(*) AS n FROM submissions WHERE owner_id=? AND session_id=?').get(owner, sessionId).n >= perSession) throw new Error('SESSION_GENERATION_LIMIT');
      if (lifetime !== -1 && db.prepare('SELECT COUNT(*) AS n FROM submissions').get().n >= lifetime) throw new Error('GENERATION_BUDGET_EXHAUSTED');
      db.prepare('INSERT INTO submissions VALUES (?,?,?,?,?,?)').run(key, owner, sessionId, requestKey, jobId, new Date().toISOString());
      db.exec('COMMIT'); return jobId;
    } catch (e) { db.exec('ROLLBACK'); throw e; }
  }
  return { register, search, model, preview, session, validSession, reserve, ensureSpace,
    get: (id, owner) => view(rowFor(id), owner),
    list: owner => db.prepare('SELECT * FROM creations WHERE retired=0 ORDER BY generated_at DESC LIMIT 100').all().filter(row => access(row, owner)).map(row => view(row, owner)),
    publish: (id, owner) => { const row = rowFor(id); if (!row || row.owner_id !== owner) throw new Error('CREATION_NOT_FOUND'); db.prepare("UPDATE creations SET visibility='shared',reuse_permission='approved_shared_collection' WHERE id=?").run(id); return view(rowFor(id), owner); },
    retire: (id, owner) => { const row = rowFor(id); if (!row || row.owner_id !== owner) throw new Error('CREATION_NOT_FOUND'); db.prepare('UPDATE creations SET retired=1 WHERE id=?').run(id); },
    submissionCount: () => db.prepare('SELECT COUNT(*) AS n FROM submissions').get().n,
    priorSubmission: (owner, sessionId, key, requestKey) => db.prepare('SELECT job_id FROM submissions WHERE owner_id=? AND (idempotency_key=? OR (session_id=? AND request_key=?)) ORDER BY created_at LIMIT 1').get(owner, key, sessionId, requestKey)?.job_id,
    close: () => db.close()
  };
}
