import express from 'express';
import { timingSafeEqual } from 'node:crypto';
import { mkdir, readFile, writeFile, rename } from 'node:fs/promises';
import path from 'node:path';
import { MAX_BYTES, validateGlb } from './glb.js';

export const CAT_PROMPT = 'Cute very chubby orange tabby cat, rounded body, short legs, fluffy cheeks, tiny paws, closed smiling eyes, cream belly, soft pastel colors, stylized 3D game asset, friendly and relaxing, suitable for a cozy VR wellness experience.';
const API = 'https://openapi.tripo3d.ai/v3';
const ID = /^[a-zA-Z0-9_-]{1,128}$/;
class SafeError extends Error {
  constructor(code, status = 502) { super(code); this.status = status; }
}

export async function createApp({ key, token, enabled = false, dataDir, hosts = ['tripo3d.ai'],
  fetchFn = fetch, pollMs = 2500, taskTimeoutMs = 600000, requestTimeoutMs = 30000 }) {
  if (!token || token.length < 32 || token === key || token.startsWith('replace_'))
    throw new Error('Configure a distinct BACKEND_ACCESS_TOKEN of at least 32 characters.');
  await mkdir(dataDir, { recursive: true });
  const journal = path.join(dataDir, 'test-cat.json');
  const modelPath = path.join(dataDir, 'test-cat.glb');
  let job = null;
  try { job = JSON.parse(await readFile(journal, 'utf8')); }
  catch (e) { if (e.code !== 'ENOENT') throw new Error('Task journal unreadable; refusing paid submission.'); }
  let submitting = false;
  let worker = null;
  const save = async () => {
    await writeFile(journal + '.tmp', JSON.stringify(job));
    await rename(journal + '.tmp', journal);
  };
  async function limitedBody(response, limit) {
    if (Number(response.headers.get('content-length')) > limit) throw new SafeError('RESPONSE_TOO_LARGE');
    let total = 0;
    const chunks = [];
    for await (const chunk of response.body) {
      total += chunk.length;
      if (total > limit) throw new SafeError('RESPONSE_TOO_LARGE');
      chunks.push(chunk);
    }
    return Buffer.concat(chunks);
  }
  async function api(route, body) {
    if (!key || key.startsWith('replace_')) throw new SafeError('TRIPO_KEY_NOT_CONFIGURED', 503);
    try {
      const response = await fetchFn(API + route, {
        method: body ? 'POST' : 'GET', redirect: 'error',
        headers: { Authorization: `Bearer ${key}`, 'Content-Type': 'application/json' },
        ...(body ? { body: JSON.stringify(body) } : {}), signal: AbortSignal.timeout(requestTimeoutMs)
      });
      const data = JSON.parse((await limitedBody(response, 256 * 1024)).toString('utf8'));
      if (response.status === 401 || [2, 1002].includes(data.code)) throw new SafeError('TRIPO_AUTH_FAILED', 502);
      if (data.code === 2010) throw new SafeError('INSUFFICIENT_CREDITS', 402);
      if (response.status === 429) throw new SafeError('TRIPO_RATE_LIMITED', 503);
      if (!response.ok || data.code !== 0 || !data.data) throw new SafeError('TRIPO_REQUEST_REJECTED');
      return data.data;
    } catch (e) {
      if (e instanceof SafeError) throw e;
      throw new SafeError(e.name === 'TimeoutError' ? 'UPSTREAM_TIMEOUT' : 'UPSTREAM_NETWORK_OR_INVALID_RESPONSE');
    }
  }
  async function balance() {
    const data = await api('/account/balance');
    if (!Number.isFinite(data.balance) || !Number.isFinite(data.frozen)) throw new SafeError('INVALID_BALANCE');
    return { balance: data.balance, frozen: data.frozen };
  }
  async function download(url) {
    const u = new URL(url);
    if (u.protocol !== 'https:' || u.username || u.password || (u.port && u.port !== '443') ||
        !hosts.some(h => u.hostname === h || u.hostname.endsWith('.' + h))) throw new SafeError('MODEL_HOST_REJECTED');
    // No Tripo Authorization header is forwarded to the CDN; redirects are forbidden.
    const response = await fetchFn(u, { redirect: 'error', signal: AbortSignal.timeout(requestTimeoutMs) });
    if (!response.ok) throw new SafeError('MODEL_DOWNLOAD_FAILED');
    const bytes = await limitedBody(response, MAX_BYTES);
    let metadata;
    try { metadata = await validateGlb(bytes); } catch { throw new SafeError('MODEL_REJECTED'); }
    await writeFile(modelPath + '.tmp', bytes);
    await rename(modelPath + '.tmp', modelPath);
    return metadata;
  }
  async function run() {
    try {
      while (Date.now() < job.deadline) {
        try {
          const task = await api('/tasks/' + encodeURIComponent(job.task_id));
          if (task.task_id !== job.task_id || typeof task.status !== 'string') throw new SafeError('INVALID_TASK');
          if (['failed', 'cancelled', 'banned', 'expired'].includes(task.status)) throw new SafeError('TASK_' + task.status.toUpperCase());
          if (task.status === 'success') {
            job.status = 'downloading'; await save();
            job.metadata = await download(task.output?.model_url);
            job.status = 'awaiting_review'; job.progress = 100; job.error = null; await save(); return;
          }
          if (!['queued', 'running', 'processing', 'pending'].includes(task.status)) throw new SafeError('INVALID_TASK_STATUS');
          job.status = 'polling';
          job.progress = Number.isFinite(task.progress) ? Math.max(0, Math.min(100, task.progress)) : 0;
          job.error = null; await save();
        } catch (e) {
          if (!['UPSTREAM_TIMEOUT', 'UPSTREAM_NETWORK_OR_INVALID_RESPONSE', 'TRIPO_RATE_LIMITED', 'MODEL_DOWNLOAD_FAILED'].includes(e.message)) throw e;
          job.error = e.message; await save();
        }
        await new Promise(resolve => setTimeout(resolve, pollMs));
      }
      throw new SafeError('TASK_TIMEOUT');
    } catch (e) {
      job.status = 'failed'; job.error = e instanceof SafeError ? e.message : 'MODEL_DOWNLOAD_OR_STORAGE_FAILED';
      await save();
    }
  }
  const startWorker = () => {
    if (!worker) worker = run().catch(() => { job.status = 'failed'; job.error = 'STORAGE_FAILED'; }).finally(() => { worker = null; });
  };
  if (job?.task_id && ['polling', 'downloading'].includes(job.status)) startWorker();
  const app = express();
  app.disable('x-powered-by');
  app.use((req, res, next) => {
    res.set({ 'Cache-Control': 'no-store', 'X-Content-Type-Options': 'nosniff' });
    if (req.path === '/health' && req.method === 'GET') return next();
    // Block browser cross-site calls (there is deliberately no CORS support).
    if (req.headers.origin || req.headers['sec-fetch-site'] === 'cross-site') return res.status(403).json({ error: 'ORIGIN_REJECTED' });
    const supplied = Buffer.from(req.get('X-Backend-Token') ?? '');
    const expected = Buffer.from(token);
    if (supplied.length !== expected.length || !timingSafeEqual(supplied, expected)) return res.status(401).json({ error: 'BACKEND_AUTH_REQUIRED' });
    next();
  });
  app.use(express.json({ limit: '2kb' }));
  app.get('/health', (req, res) => res.json({ status: 'ok' }));
  app.get('/api/balance', async (req, res) => res.json(await balance()));
  app.post('/api/test-cat', async (req, res) => {
    if (req.body?.confirm !== 'GENERATE_ONE_CAT') throw new SafeError('EXPLICIT_CONFIRMATION_REQUIRED', 400);
    if (job) return res.status(job.task_id ? 200 : 409).json(publicJob());
    if (submitting) throw new SafeError('SUBMISSION_IN_PROGRESS', 409);
    if (!enabled) throw new SafeError('GENERATION_DISABLED', 403);
    submitting = true;
    try {
      if ((await balance()).balance <= 0) throw new SafeError('INSUFFICIENT_CREDITS', 402);
      // Persist BEFORE the paid POST. Ambiguous responses never get automatically retried.
      job = { status: 'submission_unknown', task_id: null, progress: 0, error: null, deadline: Date.now() + taskTimeoutMs };
      await save();
      const task = await api('/generation/text-to-model', { prompt: CAT_PROMPT, model: 'P1-20260311', face_limit: 5000, texture: true, pbr: true });
      if (!ID.test(task.task_id ?? '')) throw new SafeError('INVALID_TASK_ID');
      job.task_id = task.task_id; job.status = 'polling'; await save(); startWorker();
      res.status(202).json(publicJob());
    } catch (e) {
      if (job) { job.error = e instanceof SafeError ? e.message : 'SUBMISSION_UNKNOWN'; await save(); }
      throw e;
    } finally { submitting = false; }
  });
  function publicJob() {
    return { task_id: job?.task_id ?? '', status: job?.status ?? 'none', progress: job?.progress ?? 0,
      error: job?.error ?? '', metadata: job?.metadata ?? null };
  }
  app.get('/api/test-cat', (req, res) => res.json(publicJob()));
  app.post('/api/test-cat/review', async (req, res) => {
    if (worker || job?.status !== 'awaiting_review') throw new SafeError('NOT_READY_FOR_REVIEW', 409);
    if (!['APPROVE', 'REJECT'].includes(req.body?.decision)) throw new SafeError('REVIEW_DECISION_REQUIRED', 400);
    job.status = req.body.decision === 'APPROVE' ? 'ready' : 'rejected'; await save(); res.json(publicJob());
  });
  app.get('/api/test-cat/model', async (req, res) => {
    if (job?.status !== 'ready') throw new SafeError('MODEL_NOT_APPROVED', 409);
    const bytes = await readFile(modelPath);
    try { await validateGlb(bytes); } catch { throw new SafeError('MODEL_REJECTED'); }
    res.type('model/gltf-binary').send(bytes);
  });
  app.use((req, res) => res.status(404).json({ error: 'NOT_FOUND' }));
  app.use((err, req, res, next) => {
    if (res.headersSent) return next(err);
    res.status(err instanceof SafeError ? err.status : 500).json({ error: err instanceof SafeError ? err.message : 'INTERNAL_OR_INVALID_REQUEST' });
  });
  return { app, waitForWorker: () => worker };
}
