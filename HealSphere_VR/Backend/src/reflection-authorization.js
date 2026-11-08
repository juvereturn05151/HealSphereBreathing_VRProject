import { readFile, stat } from 'node:fs/promises';
// Written only by the local operator after separate owner authorization. Never issued by a player-facing endpoint.
export async function reflectionAuthorization(file, proposal, proposalId, owner) {
  try {
    if ((await stat(file)).size > 16384) throw new Error();
    const grant = JSON.parse(await readFile(file, 'utf8'));
    if (!/^[a-f0-9-]{36}$/.test(grant.id) || grant.owner !== owner || grant.plan_id !== proposal.reflection_plan ||
        !Array.isArray(grant.proposals) || grant.proposals.length > 3 || !grant.proposals.includes(proposalId) ||
        !Number.isSafeInteger(grant.max_tasks) || grant.max_tasks < 1 || grant.max_tasks > 3 ||
        !Number.isSafeInteger(grant.credit_budget) || grant.credit_budget < 1 || grant.credit_budget > 10000 ||
        !Number.isSafeInteger(grant.verified_task_credit_ceiling) || grant.verified_task_credit_ceiling < 1 || grant.verified_task_credit_ceiling > grant.credit_budget ||
        !Number.isFinite(grant.expires_at) || grant.expires_at < Date.now() || grant.expires_at > Date.now() + 3600000)
      throw new Error();
    return { id: grant.id, maxTasks: grant.max_tasks, creditBudget: grant.credit_budget, creditCeiling: grant.verified_task_credit_ceiling };
  } catch { throw new Error('REFLECTION_TASK_AND_CREDIT_AUTHORIZATION_REQUIRED'); }
}
