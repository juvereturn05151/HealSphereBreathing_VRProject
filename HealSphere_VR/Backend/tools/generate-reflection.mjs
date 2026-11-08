// OPERATOR-ONLY. Preview is read-only. The confirm flag may spend credits and needs separate owner authorization.
import { randomUUID, createHash } from 'node:crypto';
import { writeFile } from 'node:fs/promises';
import path from 'node:path';
import { setTimeout as delay } from 'node:timers/promises';
import { configuration } from '../src/config.js';
const config = configuration(), args = process.argv.slice(2);
const option = key => { const at = args.indexOf(key); return at < 0 ? undefined : args[at + 1]; };
const base = 'http://127.0.0.1:' + (process.env.PORT || 3000);
async function request(route, body) {
  const response = await fetch(base + route, { method: body ? 'POST' : 'GET', headers: { 'X-Backend-Token': config.token, 'Content-Type': 'application/json' },
    ...(body ? { body: JSON.stringify(body) } : {}), signal: AbortSignal.timeout(45000) });
  const result = await response.json(); if (!response.ok) throw new Error(result.error || 'BACKEND_REQUEST_FAILED'); return result;
}
try {
  const planId = option('--plan');
  if (!planId) { console.log(JSON.stringify(await request('/api/reflection/plans'), null, 2)); process.exit(0); }
  if (!/^[a-f0-9-]{36}$/.test(planId)) throw new Error('INVALID_PLAN');
  const plan = await request('/api/reflection/plans/' + planId), missing = plan.designs.filter(d => !d.modelEndpoint);
  const maximum = Number(option('--max-tasks')), credits = Number(option('--credit-budget')), ceiling = Number(option('--verified-task-credit-ceiling'));
  console.log(JSON.stringify({ plan_id: plan.id, animal: plan.animal, missing: missing.map(d => ({ role: d.role, description: d.description })), maxTasks: Number.isFinite(maximum) ? maximum : null, creditBudget: Number.isFinite(credits) ? credits : null }, null, 2));
  if (!args.includes('--confirm-paid-generation')) { console.log('PREVIEW ONLY. No authorization written and no generation submitted.'); process.exit(0); }
  if (!Number.isSafeInteger(maximum) || maximum < 1 || maximum > 3 || !Number.isSafeInteger(credits) || credits < 1 || credits > 10000 ||
      !Number.isSafeInteger(ceiling) || ceiling < 1 || ceiling > credits) throw new Error('EXPLICIT_TASK_CREDIT_BUDGET_AND_VERIFIED_PRICE_CEILING_REQUIRED');
  const count = Math.min(maximum, missing.length, Math.floor(credits / ceiling));
  if (!count) { console.log('No authorized missing models to submit.'); process.exit(0); }
  const grant = { id: randomUUID(), owner: createHash('sha256').update(config.token).digest('hex'), plan_id: plan.id,
    max_tasks: count, credit_budget: credits, verified_task_credit_ceiling: ceiling,
    proposals: missing.slice(0, count).map(d => d.proposal_id), expires_at: Date.now() + 3600000 - 1000 };
  await writeFile(path.join(config.dataDir, 'reflection-authorization.json'), JSON.stringify(grant), { flag: 'wx', mode: 0o600 });
  for (const design of missing.slice(0, count)) {
    // Deliberately no retries of this POST. Durable backend idempotency protects a repeated explicit invocation.
    const job = await request('/api/creations', { proposal_id: design.proposal_id, idempotency_key: design.proposal_id, mode: 'live', confirm: 'CREATE_ONE_MODEL' });
    console.log(JSON.stringify({ role: design.role, job_id: job.id, task_id: job.task_id, status: job.status }));
    const deadline = Date.now() + config.taskTimeoutMs;
    let current = job;
    while (!['ready', 'failed', 'cancelled', 'rejected', 'submission_unknown', 'awaiting_review'].includes(current.status) && Date.now() < deadline) {
      await delay(3000); current = await request('/api/creations/' + job.id);
    }
    console.log(JSON.stringify({ role: design.role, status: current.status, creation_id: current.creation_id }));
    if (current.status !== 'ready') throw new Error('STOPPED_WITH_EXISTING_TASK_DO_NOT_RESUBMIT');
  }
} catch (e) {
  // Print only controlled codes. Node fetch exceptions/provider bodies can contain private details.
  console.error(/^[A-Z0-9_]+$/.test(e.message ?? '') ? e.message : 'OPERATOR_OPERATION_FAILED_CHECK_PRIVATE_JOURNAL'); process.exitCode = 1;
}
