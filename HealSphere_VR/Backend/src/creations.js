import { randomUUID } from 'node:crypto';
import { mkdir, readdir, readFile, writeFile, rename } from 'node:fs/promises';
import path from 'node:path';
import { setTimeout as delay } from 'node:timers/promises';
import { createConversation, safeText } from './conversation.js';
import { MAX_BYTES, validateGlb } from './glb.js';
import { requestKey, requirements } from './matching.js';
import { imageSize } from 'image-size';
import { reflectionAuthorization } from './reflection-authorization.js';
import { prepareAnimal } from './animal-request.js';

export async function mountCreations(app, { api, balance, downloadBytes, limitedBody, SafeError, dataDir, enabled, fetchFn,
  pollMs, taskTimeoutMs, openaiKey, openaiModel, offlineConversation = true, reviewMode = 'openai', demoModelPath, demoDelayMs = 2500, maxJobs = 32, library, limits, owner }) {
  const root = path.join(dataDir, 'creations'); await mkdir(root, { recursive: true });
  const conversation = createConversation({ openaiKey, openaiModel, fetchFn, offline: offlineConversation });
  const jobs = new Map(), workers = new Map(), controllers = new Map();
  const idPattern = /^[a-f0-9-]{36}$/;
  const file = (id, ext) => path.join(root, id + ext);
  const save = async job => { await writeFile(file(job.id, '.json.tmp'), JSON.stringify(job)); await rename(file(job.id, '.json.tmp'), file(job.id, '.json')); };
  for (const name of await readdir(root)) if (/^[a-f0-9-]{36}\.json$/.test(name)) {
    const job = JSON.parse(await readFile(path.join(root, name), 'utf8'));
    if (!idPattern.test(job.id)) throw new Error('Invalid creation journal.');
    // Reuse handles are ephemeral; permanent assets and paid submissions live in SQLite.
    if (job.source === 'library') continue;
    if (jobs.size >= maxJobs) throw new Error('Creation journal requires operator maintenance.');
    if (!job.owner_id || job.owner_id === owner) jobs.set(job.id, job);
  }
  const view = job => ({ id: job.id, task_id: job.task_id ?? '', displayName: job.displayName, status: job.status,
    progress: job.progress ?? 0, error: job.error ?? '', source: job.source, review: job.review ?? '', metadata: job.metadata ?? null,
    creation_id: job.creation_id ?? '', model_source: job.model_source ?? (job.source === 'demo' ? 'prebuilt' : 'AI-generated') });
  const persistModel = async (job, bytes, preview) => {
    const asset = await library.register({ owner, bytes, originalRequest: job.originalRequest ?? job.displayName,
      prompt: job.prompt ?? job.displayName, displayName: job.displayName, provider: job.source === 'demo' ? 'project' : 'tripo', taskId: job.task_id,
      source: job.source === 'demo' ? 'prebuilt' : 'AI-generated', review: job.review, preview,
      verifiedAttributes: job.source === 'demo' ? requirements('Cute chubby orange striped cat seated stylized low-poly pastel') : undefined,
      previewExtension: job.preview_mime === 'image/jpeg' ? 'jpg' : 'png' });
    job.creation_id = asset.id; job.model_source = asset.source;
  };
  if (demoModelPath) {
    const prepared = await library.register({ owner, bytes: await readFile(demoModelPath), originalRequest: 'Cute chubby orange striped cat seated',
      prompt: 'Cute chubby orange striped cat seated, stylized low-poly pastel game asset', displayName: 'Prebuilt demo cat', provider: 'project', source: 'prebuilt',
      review: 'reviewed_local_demo', rights: 'existing_project_asset_prototype_use' });
    if (limits.sharedCollection) library.publish(prepared.id, owner);
  }
  async function readyWithoutVisualReview(job, bytes) {
    job.review = 'skipped_by_owner';
    await persistModel(job, bytes);
    job.status = 'ready'; job.progress = 100; job.error = '';
    await save(job);
  }
  async function run(job, signal) {
    try {
      if (job.source === 'demo') {
        await delay(demoDelayMs, undefined, { signal });
        const bytes = await readFile(demoModelPath); job.metadata = await validateGlb(bytes, { maxTriangles: 60000 });
        signal.throwIfAborted(); await writeFile(file(job.id, '.glb'), bytes);
        job.status = 'ready'; job.progress = 100; job.review = 'reviewed_local_demo'; await persistModel(job, bytes); await save(job); return;
      }
      while (Date.now() < job.deadline) {
        signal.throwIfAborted();
        try {
          const task = await api('/tasks/' + encodeURIComponent(job.task_id), undefined, signal);
          if (task.task_id !== job.task_id) throw new SafeError('INVALID_TASK');
          if (['failed', 'cancelled', 'banned', 'expired'].includes(task.status)) throw new SafeError('GENERATION_FAILED');
          if (task.status === 'success') {
            library.recordCredits(job.id, task.credits_consumed);
            job.status = 'downloading'; await save(job);
            const { bytes, metadata } = await downloadBytes(task.output?.model_url, signal);
            signal.throwIfAborted(); await library.ensureSpace(bytes.length + 8192); await writeFile(file(job.id, '.glb.tmp'), bytes); await rename(file(job.id, '.glb.tmp'), file(job.id, '.glb')); job.metadata = metadata;
            if (reviewMode === 'none') { await readyWithoutVisualReview(job, bytes); return; }
            job.status = 'awaiting_review'; job.progress = 100; job.review = 'operator_review_required'; await save(job);
            if (reviewMode === 'openai' && conversation.configured && task.output?.rendered_image_url) {
              try {
                const url = new URL(task.output.rendered_image_url);
                // Use the same CDN host validation as the GLB download, with no credentials forwarded.
                await downloadPreview(url, job, signal);
              } catch (e) { if (signal.aborted) throw e; job.error = 'VISUAL_REVIEW_REQUIRED'; }
              await save(job);
            }
            if (job.status === 'ready') { await persistModel(job, bytes, job.preview_mime ? await readFile(file(job.id, '.preview')).catch(() => undefined) : undefined); await save(job); }
            return;
          }
          if (!['queued', 'running', 'processing', 'pending'].includes(task.status)) throw new SafeError('INVALID_TASK_STATUS');
          job.status = 'generating'; job.progress = Number.isFinite(task.progress) ? Math.max(0, Math.min(99, task.progress)) : 0;
          job.error = ''; await save(job);
        } catch (e) {
          if (signal.aborted) throw e;
          if (!['UPSTREAM_TIMEOUT', 'UPSTREAM_NETWORK_OR_INVALID_RESPONSE', 'TRIPO_RATE_LIMITED', 'MODEL_DOWNLOAD_FAILED'].includes(e.message)) throw e;
          job.error = e.message; await save(job);
        }
        await delay(pollMs, undefined, { signal });
      }
      throw new SafeError('GENERATION_TIMEOUT');
    } catch (e) {
      if (signal.aborted) { job.status = 'cancelled'; job.error = 'CANCELLED_LOCALLY_REMOTE_TASK_MAY_CONTINUE'; }
      else { job.status = 'failed'; job.error = e instanceof SafeError ? e.message : 'GENERATION_OR_STORAGE_FAILED'; }
      await save(job);
    }
  }
  async function downloadPreview(url, job, signal) {
    // downloadBytes exposes its host validator, shared with the existing safe downloader.
    downloadBytes.validateUrl(url);
    const response = await fetchFn(url, { redirect: 'error', signal: AbortSignal.any([signal, AbortSignal.timeout(30000)]) });
    const declared = response.headers.get('content-type')?.split(';')[0];
    if (!response.ok || !['image/png', 'image/jpeg', 'application/octet-stream', 'binary/octet-stream'].includes(declared)) throw new SafeError('VISUAL_REVIEW_REQUIRED');
    const preview = await limitedBody(response, 4 * 1024 * 1024);
    const dimensions = imageSize(preview);
    const mime = dimensions.type === 'png' ? 'image/png' : dimensions.type === 'jpg' ? 'image/jpeg' : null;
    if (!mime || dimensions.width > 2048 || dimensions.height > 2048 || declared.startsWith('image/') && declared !== mime) throw new SafeError('VISUAL_REVIEW_REQUIRED');
    await library.ensureSpace(preview.length + 8192); await writeFile(file(job.id, '.preview.tmp'), preview); await rename(file(job.id, '.preview.tmp'), file(job.id, '.preview')); job.preview_mime = mime;
    const approved = await conversation.reviewImage(preview, mime, job.prompt ?? job.displayName, signal);
    signal.throwIfAborted(); job.status = approved ? 'ready' : 'rejected'; job.review = 'openai_visual_review'; job.error = approved ? '' : 'MODEL_REVIEW_REJECTED';
  }
  function start(job) {
    if (workers.has(job.id)) return;
    const controller = new AbortController(); controllers.set(job.id, controller);
    const promise = run(job, controller.signal).catch(() => { job.status = 'failed'; job.error = 'STORAGE_FAILED'; })
      .finally(() => { workers.delete(job.id); controllers.delete(job.id); });
    workers.set(job.id, promise);
  }
  for (const job of jobs.values()) if (['generating', 'downloading'].includes(job.status) &&
      (job.task_id || job.source === 'demo' && demoModelPath)) start(job);
  for (const job of jobs.values()) if (job.status === 'ready' && job.source !== 'library' && !job.creation_id) {
    await persistModel(job, await readFile(file(job.id, '.glb'))); await save(job);
  }
  if (reviewMode === 'none') for (const job of jobs.values()) if (job.status === 'awaiting_review') {
    try {
      const bytes = await readFile(file(job.id, '.glb'));
      job.metadata = await validateGlb(bytes, { maxTriangles: job.source === 'demo' ? 60000 : 20000 });
      await readyWithoutVisualReview(job, bytes);
    } catch { job.status = 'failed'; job.error = 'MODEL_REJECTED'; await save(job); }
  }
  app.get('/api/capabilities', (req, res) => res.json({ conversation: conversation.configured ? 'openai' : offlineConversation ? 'offline' : 'unconfigured', liveGenerationEnabled: !!enabled, demoAvailable: !!demoModelPath, reviewMode,
    generationBudget: { used: library.submissionCount(), limit: limits.maxLifetime === -1 ? null : limits.maxLifetime,
      unlimited: limits.maxLifetime === -1, perSessionLimit: limits.perSession === -1 ? null : limits.perSession } }));
  app.post('/api/sessions', (req, res) => res.json({ session_id: library.session(owner) }));
  app.get('/api/library', (req, res) => res.json({ creations: library.list(owner) }));
  app.post('/api/library/search', (req, res) => {
    if (!safeText(req.body?.idea)) throw new SafeError('INVALID_IDEA', 400);
    res.json({ creations: library.search(req.body.idea, owner) });
  });
  app.get('/api/library/:id', (req, res) => { const asset = library.get(req.params.id, owner); if (!asset) throw new SafeError('CREATION_NOT_FOUND', 404); res.json(asset); });
  app.get('/api/library/:id/model', async (req, res) => {
    try { res.type('model/gltf-binary').send(await library.model(req.params.id, owner)); }
    catch (e) { throw new SafeError(e.message === 'CREATION_NOT_FOUND' ? e.message : 'MODEL_REJECTED', 404); }
  });
  app.get('/api/library/:id/preview', async (req, res) => {
    try { const preview = await library.preview(req.params.id, owner); res.type(preview.mime).send(preview.bytes); }
    catch { throw new SafeError('CREATION_NOT_FOUND', 404); }
  });
  app.post('/api/conversation', async (req, res) => {
    const controller = new AbortController(); res.on('close', () => { if (!res.writableEnded) controller.abort(); });
    try {
      const result = await conversation.understand(req.body?.idea, req.body?.clarification_id, controller.signal);
      if (result.accepted && !result.needsClarification) {
        const proposal = conversation.proposal(result.proposal_id);
        const match = library.search(proposal.originalRequest, owner)[0]; result.saved_creation_id = match?.id ?? '';
      }
      res.json(result);
    }
    catch (e) { throw new SafeError(['INVALID_OR_UNSUPPORTED_IDEA', 'IDEA_TOO_LONG'].includes(e.message) ? e.message :
      ['CONTENT_NOT_ACCEPTED','CONVERSATION_BUSY','CLARIFICATION_EXPIRED','OPENAI_NOT_CONFIGURED','OPENAI_AUTH_FAILED','INVALID_CONVERSATION_RESPONSE'].includes(e.message) ? e.message : 'OPENAI_UNAVAILABLE', 400); }
  });
  let submitting = false;
  const cancelledProposals = new Set();
  const animalProposals = new Map();
  app.post('/api/animals/prepare', (req, res) => {
    const animal = prepareAnimal(req.body?.animal);
    if (!animal) throw new SafeError('INVALID_ANIMAL', 400);
    const match = library.search(animal.description, owner)[0];
    if (match) return res.json({ modelEndpoint: match.modelEndpoint, prebuilt: match.source === 'prebuilt' });
    for (const [id, proposal] of animalProposals) if (proposal.expires <= Date.now()) animalProposals.delete(id);
    if (animalProposals.size >= 32) throw new SafeError('CREATION_CAPACITY_REACHED', 409);
    const id = randomUUID();
    animalProposals.set(id, { originalRequest: animal.description, prompt: animal.prompt, displayName: animal.displayName,
      reflection_plan: id, expires: Date.now() + 15 * 60000 });
    res.json({ proposal_id: id });
  });
  app.get('/api/animals/prepare', (req, res) => {
    res.json({ pending: [...animalProposals].filter(([, proposal]) => proposal.expires > Date.now())
      .map(([id, proposal]) => ({ proposal_id: id, animal: proposal.originalRequest })) });
  });
  app.post('/api/creations', async (req, res) => {
    const proposalId = req.body?.proposal_id;
    const existing = [...jobs.values()].find(j => j.proposal_id === proposalId);
    if (existing) return res.status(existing.status === 'submission_unknown' ? 409 : 200).json(view(existing));
    if (!idPattern.test(proposalId ?? '') || !['live','reuse','demo'].includes(req.body?.mode)) throw new SafeError('INVALID_CREATION_REQUEST', 400);
    if (req.body?.session_id !== undefined && !library.validSession(req.body.session_id, owner)) throw new SafeError('INVALID_SESSION', 400);
    if (req.body?.idempotency_key !== undefined && (typeof req.body.idempotency_key !== 'string' || !/^[a-zA-Z0-9_-]{8,128}$/.test(req.body.idempotency_key))) throw new SafeError('INVALID_IDEMPOTENCY_KEY', 400);
    const animalProposal = animalProposals.get(proposalId);
    const proposal = conversation.proposal(proposalId) ?? (animalProposal?.expires > Date.now() ? animalProposal : null);
    if (!proposal) throw new SafeError('PROPOSAL_EXPIRED', 409);
    if (cancelledProposals.has(proposalId)) throw new SafeError('PROPOSAL_CANCELLED', 409);
    const match = library.search(proposal.originalRequest, owner)[0];
    if (match && req.body?.mode !== 'demo') {
      if (submitting) throw new SafeError('SUBMISSION_IN_PROGRESS', 409);
      submitting = true;
      try {
      const reused = { id: randomUUID(), proposal_id: proposalId, displayName: match.displayName, source: 'library', status: 'ready', progress: 100,
        owner_id: owner, review: match.review, creation_id: match.id, model_source: match.source, metadata: match.validation, task_id: match.task_id };
      const reusable = [...jobs.values()].filter(j => j.source === 'library');
      if (reusable.length >= 64) jobs.delete(reusable[0].id);
      jobs.set(reused.id, reused); return res.json(view(reused));
      } finally { submitting = false; }
    }
    if (req.body?.mode === 'reuse') throw new SafeError('SAVED_CREATION_UNAVAILABLE', 409);
    if (req.body?.confirm !== 'CREATE_ONE_MODEL') throw new SafeError('EXPLICIT_CONFIRMATION_REQUIRED', 400);
    const demo = req.body?.mode === 'demo';
    if (!demo && req.body?.mode !== 'live') throw new SafeError('MODE_REQUIRED', 400);
    if (demo && !demoModelPath) throw new SafeError('DEMO_MODEL_NOT_CONFIGURED', 503);
    if (!demo) {
      const prior = library.priorSubmission(owner, req.body?.session_id ?? proposalId, req.body?.idempotency_key ?? proposalId, requestKey(proposal.originalRequest));
      if (prior) { const priorJob = jobs.get(prior); if (!priorJob) throw new SafeError('SUBMISSION_UNKNOWN_DO_NOT_RESUBMIT', 409); return res.json(view(priorJob)); }
    }
    let authorization;
    if (!demo && (proposal.reflection || (animalProposal && !enabled))) {
      const receipt = animalProposal ? 'animal-authorization.json' : 'reflection-authorization.json';
      try { authorization = await reflectionAuthorization(path.join(dataDir, receipt), proposal, proposalId, owner); }
      catch { throw new SafeError(animalProposal ? 'GENERATION_DISABLED' : 'REFLECTION_TASK_AND_CREDIT_AUTHORIZATION_REQUIRED', 403); }
    }
    if (!demo && !enabled && !authorization) throw new SafeError('GENERATION_DISABLED', 403);
    if (submitting || workers.size >= limits.maxConcurrent || [...jobs.values()].filter(j => j.source !== 'library').length >= maxJobs) throw new SafeError('CREATION_CAPACITY_REACHED', 409);
    submitting = true;
    try {
      if (!demo && (await balance()).balance < (authorization?.creditCeiling ?? 1)) throw new SafeError('INSUFFICIENT_CREDITS', 402);
      if (cancelledProposals.has(proposalId)) throw new SafeError('PROPOSAL_CANCELLED', 409);
      await library.ensureSpace(limits.maxModelBytes * 2 + 8 * 1024 * 1024 + 16384);
      const prompt = limits.authorizedPrompt ?? proposal.prompt;
      if (!demo && (!limits.allowedProviders.includes('tripo') || prompt.length > 1024)) throw new SafeError('PROVIDER_OR_PROMPT_NOT_ALLOWED', 400);
      if (!demo && limits.authorizedPrompt && requestKey(proposal.originalRequest) !== requestKey(limits.authorizedPrompt)) throw new SafeError('PROMPT_NOT_AUTHORIZED', 403);
      const job = { id: randomUUID(), proposal_id: proposalId, displayName: demo ? 'Prepared Orange Cat (offline demo)' : proposal.displayName, source: demo ? 'demo' : 'tripo',
        originalRequest: proposal.originalRequest, prompt, owner_id: owner,
        status: demo ? 'generating' : 'submission_unknown', progress: 0, deadline: Date.now() + taskTimeoutMs, error: '', task_id: '' };
      await save(job); jobs.set(job.id, job);
      if (!demo) {
        const sessionId = req.body?.session_id ?? proposalId;
        if (req.body?.session_id && !library.validSession(sessionId, owner)) { jobs.delete(job.id); throw new SafeError('INVALID_SESSION', 400); }
        const key = req.body?.idempotency_key ?? proposalId;
        if (typeof key !== 'string' || !/^[a-zA-Z0-9_-]{8,128}$/.test(key)) throw new SafeError('INVALID_IDEMPOTENCY_KEY', 400);
        let prior;
        try { prior = library.reserve({ key, owner, sessionId, requestKey: requestKey(proposal.originalRequest), jobId: job.id, perSession: limits.perSession, lifetime: limits.maxLifetime, authorization }); }
        catch (e) { job.status = 'failed'; job.error = ['SESSION_GENERATION_LIMIT','GENERATION_BUDGET_EXHAUSTED','IDEMPOTENCY_CONFLICT','REFLECTION_AUTHORIZATION_BUDGET_EXHAUSTED'].includes(e.message) ? e.message : 'STORAGE_FAILED'; await save(job); throw new SafeError(job.error, 409); }
        if (prior !== job.id) { job.status = 'cancelled'; await save(job); const priorJob = jobs.get(prior); if (!priorJob) throw new SafeError('SUBMISSION_UNKNOWN_DO_NOT_RESUBMIT', 409); return res.json(view(priorJob)); }
        try {
          const task = await api('/generation/text-to-model', { prompt, model: 'P1-20260311', face_limit: 5000, texture: true, pbr: true });
          if (!/^[a-zA-Z0-9_-]{1,128}$/.test(task.task_id ?? '')) throw new SafeError('INVALID_TASK_ID');
          job.task_id = task.task_id; job.status = cancelledProposals.has(proposalId) ? 'cancelled' : 'generating'; await save(job);
        } catch (e) { job.error = 'SUBMISSION_UNKNOWN_DO_NOT_RESUBMIT'; await save(job); throw new SafeError(job.error, 409); }
      }
      if (job.status !== 'cancelled') start(job); res.status(202).json(view(job));
    } finally { submitting = false; }
  });
  function find(req) { const job = idPattern.test(req.params.id) && jobs.get(req.params.id); if (!job) throw new SafeError('CREATION_NOT_FOUND', 404); return job; }
  app.post('/api/proposals/:id/cancel', async (req, res) => {
    if (!idPattern.test(req.params.id)) throw new SafeError('INVALID_PROPOSAL', 400);
    if (cancelledProposals.size >= 128) cancelledProposals.delete(cancelledProposals.values().next().value);
    cancelledProposals.add(req.params.id);
    const job = [...jobs.values()].find(j => j.proposal_id === req.params.id);
    if (job) { controllers.get(job.id)?.abort(); await workers.get(job.id); if (job.status === 'generating') { job.status = 'cancelled'; await save(job); } }
    res.json({ status: 'cancelled', remoteMayContinue: true });
  });
  app.get('/api/creations/:id', (req, res) => res.json(view(find(req))));
  app.post('/api/creations/:id/cancel', async (req, res) => {
    const job = find(req); controllers.get(job.id)?.abort(); await workers.get(job.id);
    if (!['ready', 'rejected', 'failed', 'submission_unknown'].includes(job.status)) { job.status = 'cancelled'; job.error = 'CANCELLED_LOCALLY_REMOTE_TASK_MAY_CONTINUE'; await save(job); }
    res.json(view(job));
  });
  app.post('/api/creations/:id/review', async (req, res) => {
    const job = find(req); if (!['awaiting_review', 'rejected'].includes(job.status) || workers.has(job.id)) throw new SafeError('NOT_READY_FOR_REVIEW', 409);
    if (!['APPROVE', 'REJECT'].includes(req.body?.decision)) throw new SafeError('REVIEW_DECISION_REQUIRED', 400);
    const override = job.status === 'rejected' && req.body.decision === 'APPROVE';
    if (override && (req.body.confirm !== 'APPROVE_REVIEWED_EXISTING_ASSET' || !safeText(req.body.note, 256)))
      throw new SafeError('EXPLICIT_REVIEW_OVERRIDE_AND_NOTE_REQUIRED', 409);
    const previousReview = job.review;
    const history = { at: new Date().toISOString(), previousStatus: job.status, previousReview, previousError: job.error,
      decision: req.body.decision, note: override ? req.body.note.trim() : 'Explicit operator review of existing asset.' };
    if (req.body.decision === 'APPROVE') {
      job.review = 'operator_review';
      try { await persistModel(job, await readFile(file(job.id, '.glb')), job.preview_mime ? await readFile(file(job.id, '.preview')).catch(() => undefined) : undefined); }
      catch { job.review = previousReview; throw new SafeError('LIBRARY_REGISTRATION_FAILED', 409); }
    }
    job.review_history = [...(job.review_history ?? []), history].slice(-32);
    job.status = req.body.decision === 'APPROVE' ? 'ready' : 'rejected'; job.review = 'operator_review'; job.error = ''; await save(job); res.json(view(job));
  });
  app.get('/api/creations/:id/model', async (req, res) => {
    const job = find(req); if (job.status !== 'ready') throw new SafeError('MODEL_NOT_APPROVED', 409);
    if (job.creation_id) {
      try { return res.type('model/gltf-binary').send(await library.model(job.creation_id, owner)); }
      catch { throw new SafeError('MODEL_REJECTED'); }
    }
    const bytes = await readFile(file(job.id, '.glb')); try { await validateGlb(bytes, { maxTriangles: job.source === 'demo' ? 60000 : 20000 }); } catch { throw new SafeError('MODEL_REJECTED'); }
    res.type('model/gltf-binary').send(bytes);
  });
  return { wait: async () => { while (workers.size) await Promise.all([...workers.values()]); }, conversation };
}
