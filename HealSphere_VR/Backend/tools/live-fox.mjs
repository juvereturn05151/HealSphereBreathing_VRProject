// Explicit operator tool, never imported by automated tests. Authorization: attachment 35da0db2-942b-4760-b312-8629110f8f02.
import { readFile, writeFile, mkdir, rename, unlink, open } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import path from 'node:path';
import { configuration } from '../src/config.js';
import { createApp } from '../src/app.js';
export const FOX_PROMPT = 'A cute little sleeping fox, rounded body, soft orange fur, fluffy tail, peaceful sleeping expression, stylized low-poly 3D model, complete body, suitable for a cozy pastel VR wellness environment.';
const authorization = 'authorized-fox-35da0db2-942b-4760-b312-8629110f8f02';
const options = configuration(); await mkdir(options.dataDir, { recursive: true });
const journalPath = path.join(options.dataDir, authorization + '.json'), lockPath = journalPath + '.lock';
let journal;
try { journal = JSON.parse(await readFile(journalPath, 'utf8')); }
catch (e) { if (e.code !== 'ENOENT') throw new Error('AUTHORIZATION_JOURNAL_UNREADABLE'); journal = { authorization, promptHash: createHash('sha256').update(FOX_PROMPT).digest('hex'), paidPostAttempted: false, paidPosts: 0 }; }
if (journal.authorization !== authorization || journal.promptHash !== createHash('sha256').update(FOX_PROMPT).digest('hex')) throw new Error('AUTHORIZATION_JOURNAL_MISMATCH');
const save = async () => {
  const temp = journalPath + '.tmp'; const handle = await open(temp, 'w', 0o600);
  try { await handle.writeFile(JSON.stringify(journal, null, 2)); await handle.sync(); } finally { await handle.close(); }
  await rename(temp, journalPath);
};
const summary = () => console.log(JSON.stringify({ status: journal.status, task_id: journal.task_id ?? '', job_id: journal.job_id ?? '',
  balanceBefore: journal.balanceBefore, frozenBefore: journal.frozenBefore, balanceAfter: journal.balanceAfter, creditsConsumed: journal.creditsConsumed,
  paidPosts: journal.paidPosts, providerHttp: journal.providerHttp, providerCode: journal.providerCode, creation_id: journal.creation_id ?? '', blocker: journal.blocker ?? '' }));
let server, backend, lock;
try {
  lock = await open(lockPath, 'wx', 0o600); await lock.writeFile(String(process.pid));
  const realFetch = globalThis.fetch;
  async function guardedFetch(address, init) {
    const url = String(address);
    if (url === 'https://openapi.tripo3d.ai/v3/generation/text-to-model') {
      if (!process.argv.includes('--submit-once') || journal.paidPostAttempted || JSON.parse(init.body).prompt !== FOX_PROMPT) throw new Error('PAID_SUBMISSION_BLOCKED');
      journal.paidPostAttempted = true; journal.paidPosts = 1; journal.status = 'submission_unknown';
      journal.job_id = backend.library.priorSubmission(createHash('sha256').update(options.token).digest('hex'), journal.session_id, authorization, '');
      await save(); // Durable and flushed BEFORE any billable external request.
      const response = await realFetch(address, init);
      const result = await response.clone().json(); journal.providerHttp = response.status; journal.providerCode = result.code;
      if (typeof result.data?.task_id === 'string' && /^[a-zA-Z0-9_-]{1,128}$/.test(result.data.task_id)) journal.task_id = result.data.task_id;
      await save(); return response;
    }
    if (init?.method === 'POST' && url.startsWith('https://openapi.tripo3d.ai/')) throw new Error('OTHER_PAID_OPERATION_BLOCKED');
    return realFetch(address, init);
  }
  // Reconcile a task ID received before an interrupted backend journal save. Never submit again.
  if (journal.job_id && journal.task_id) {
    const file = path.join(options.dataDir, 'creations', journal.job_id + '.json'); const job = JSON.parse(await readFile(file, 'utf8'));
    if (job.prompt === FOX_PROMPT && ((job.status === 'submission_unknown' && !job.task_id) ||
        process.argv.includes('--resume-download') && job.status === 'failed' && ['MODEL_HOST_REJECTED','MODEL_DOWNLOAD_FAILED'].includes(job.error) ||
        process.argv.includes('--resume-review') && job.status === 'awaiting_review')) {
      job.task_id = journal.task_id; job.status = 'generating'; job.deadline = Date.now() + options.taskTimeoutMs;
      await writeFile(file + '.tmp', JSON.stringify(job)); await rename(file + '.tmp', file);
    }
  }
  backend = await createApp({ ...options, enabled: process.argv.includes('--submit-once'), fetchFn: guardedFetch,
    generationLimits: { ...options.generationLimits, maxLifetime: 1, perSession: 1, authorizedPrompt: FOX_PROMPT } });
  server = backend.app.listen(0, '127.0.0.1'); await new Promise(r => server.once('listening', r));
  const request = async (route, body) => {
    const response = await realFetch('http://127.0.0.1:' + server.address().port + route, { method: body ? 'POST' : 'GET',
      headers: { 'X-Backend-Token': options.token, 'Content-Type': 'application/json' }, ...(body ? { body: JSON.stringify(body) } : {}), signal: AbortSignal.timeout(45000) });
    const result = await response.json(); if (!response.ok) throw new Error(/^[A-Z0-9_]+$/.test(result.error ?? '') ? result.error : 'BACKEND_REQUEST_FAILED'); return result;
  };
  const balance = await request('/api/balance'); journal.balanceBefore ??= balance.balance; journal.frozenBefore ??= balance.frozen;
  journal.lastCheckedAt = new Date().toISOString(); journal.latestBalance = balance.balance;
  journal.pricing = { url: 'https://developers.tripo3d.ai/en/pricing', model: 'P1-20260311', exactQuoteAvailable: false, note: 'No documented P1 quote endpoint. Record provider consumption after completion; no premium options requested.' };
  await save();
  if (!process.argv.includes('--submit-once') && !process.argv.includes('--resume-download') && !process.argv.includes('--resume-review')) { journal.status = journal.paidPostAttempted ? journal.status : 'preflight'; journal.blocker = balance.balance <= 0 ? 'INSUFFICIENT_CREDITS' : ''; await save(); }
  else {
    if (!journal.paidPostAttempted && balance.balance <= 0) throw new Error('INSUFFICIENT_CREDITS');
    if (!journal.paidPostAttempted) {
      // Also inspect successful provider history, without printing other private tasks.
      const usageResponse = await guardedFetch('https://openapi.tripo3d.ai/v3/account/usage', { headers: { Authorization: 'Bearer ' + options.key }, signal: AbortSignal.timeout(30000) });
      const usage = await usageResponse.json(); journal.historyChecked = usageResponse.ok && usage.code === 0;
      journal.localPriorSubmissions = backend.library.submissionCount();
      if (journal.localPriorSubmissions > 0) throw new Error('EXISTING_SUBMISSION_REQUIRES_RECONCILIATION');
      const session = await request('/api/sessions', {}); journal.session_id = session.session_id;
      const proposal = await request('/api/conversation', { idea: FOX_PROMPT });
      if (!proposal.accepted || proposal.needsClarification) throw new Error('PROMPT_NOT_ACCEPTED');
      journal.conversationSource = proposal.source; journal.proposal_id = proposal.proposal_id; journal.blocker = ''; await save();
      const job = await request('/api/creations', { proposal_id: proposal.proposal_id, session_id: journal.session_id, idempotency_key: authorization,
        mode: 'live', confirm: 'CREATE_ONE_MODEL' }); journal.job_id = job.id; journal.task_id = job.task_id; await save();
    }
    if (!journal.job_id || !journal.task_id) throw new Error('SUBMISSION_UNKNOWN_DO_NOT_RESUBMIT');
    await backend.waitForWorker(); const job = await request('/api/creations/' + journal.job_id);
    journal.status = job.status; journal.blocker = job.error; journal.creation_id = job.creation_id; journal.validation = job.metadata;
    const after = await request('/api/balance'); journal.balanceAfter = after.balance;
    const taskResponse = await guardedFetch('https://openapi.tripo3d.ai/v3/tasks/' + encodeURIComponent(journal.task_id), { headers: { Authorization: 'Bearer ' + options.key }, signal: AbortSignal.timeout(30000) });
    const task = (await taskResponse.json()).data;
    if (Number.isFinite(task?.credits_consumed)) journal.creditsConsumed = task.credits_consumed;
    journal.providerStatus = task?.status; await save();
  }
} catch (e) {
  journal.blocker = /^[A-Z0-9_]+$/.test(e.message) ? e.message : e.code === 'EEXIST' ? 'TEST_ALREADY_RUNNING_OR_STALE_LOCK' : e.cause?.code ?? 'LIVE_TEST_NETWORK_OR_CONFIGURATION_FAILURE';
  if (!journal.paidPostAttempted) journal.status = 'blocked_before_submission'; await save();
} finally {
  if (backend) { await backend.waitForWorker(); backend.close(); }
  if (server) await new Promise(r => server.close(r));
  if (lock) { await lock.close(); await unlink(lockPath); }
  summary();
}
