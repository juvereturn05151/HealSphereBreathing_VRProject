import express from 'express';
import { createReflectionService } from './reflection.js';
import { createSpeechService } from './speech.js';

export function mountReflection(app, options) {
  const { SafeError } = options;
  const reflection = createReflectionService(options);
  const speech = createSpeechService({ ...options, reflection });
  async function operation(req, res, work) {
    const controller = new AbortController(); res.on('close', () => { if (!res.writableEnded) controller.abort(); });
    try { await work(controller.signal); }
    catch (e) {
      const known = ['INVALID_REFLECTION_INPUT','DEMO_ANIMAL_UNAVAILABLE','OPENAI_NOT_CONFIGURED','OPENAI_AUTH_FAILED','REFLECTION_CONTENT_NOT_ACCEPTED','INVALID_REFLECTION_RESPONSE','DESIGNS_MUST_BE_DISTINCT','REFLECTION_CAPACITY_REACHED',
        'REFLECTION_PLAN_EXPIRED','INVALID_NARRATION_CUE','VOICE_NOT_CONFIGURED','VOICE_BUSY','INVALID_AUDIO','INVALID_AUDIO_DURATION','NO_SPEECH_DETECTED','INVALID_TRANSCRIPTION'];
      throw new SafeError(known.includes(e.message) ? e.message : 'REFLECTION_OR_VOICE_UNAVAILABLE', 400);
    }
  }
  app.post('/api/reflection/plans', (req, res) => operation(req, res, async signal => res.json(await reflection.create(req.body, signal))));
  app.get('/api/reflection/plans', (req, res) => res.json({ plans: reflection.list() }));
  app.get('/api/reflection/plans/:id', (req, res) => { const plan = reflection.get(req.params.id); if (!plan) throw new SafeError('REFLECTION_PLAN_EXPIRED', 404); res.json(plan); });
  app.post('/api/reflection/plans/:id/cancel', (req, res) => { reflection.cancel(req.params.id); speech.cancel(req.params.id); res.json({ cancelled: true }); });
  app.get('/api/reflection/demo/:animal/:role/model', async (req, res) => {
    try { res.type('model/gltf-binary').send(await reflection.demo(req.params.animal, req.params.role)); }
    catch { throw new SafeError('DEMO_MODEL_UNAVAILABLE', 404); }
  });
  app.post('/api/speech/narration', (req, res) => operation(req, res, async signal => res.json(await speech.narrate(req.body?.cue, req.body?.plan_id, signal))));
  app.get('/api/speech/audio/:id', (req, res) => { const bytes = speech.audio(req.params.id); if (!bytes) throw new SafeError('AUDIO_NOT_FOUND', 404); res.type('audio/wav').send(bytes); });
  app.post('/api/speech/transcribe', express.raw({ type: 'audio/wav', limit: '2mb' }), (req, res) => operation(req, res, async signal => res.json(await speech.transcribe(req.body, signal))));
  return { reflection, speech };
}
