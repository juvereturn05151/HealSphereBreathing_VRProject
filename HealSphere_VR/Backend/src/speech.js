import { createHash } from 'node:crypto';
import { mkdir, readFile, writeFile, rename } from 'node:fs/promises';
import path from 'node:path';
import { openaiTransport } from './openai-audio.js';

export const narrationCues = Object.freeze({
  welcome: "Hello there. I'm happy you're here. We can take things slowly.",
  breathe_in: "A gentle breath in, if comfortable.", breathe_out: "And an easy breath out.",
  animal: 'What animal would you like to see today?',
  thought1: "What's something that's been on your mind lately? You can skip sharing.",
  thought2: 'Thank you for sharing. Is there anything else you have been thinking about? You can skip this too.',
  generation: 'Thank you. We can give these little friends time to take shape.',
  reflection: 'This resting friend offers a little invitation to pause.',
  support: 'This one is here to offer a little comfort.', hope: 'And this one leaves room for a hopeful possibility.',
  closing: "Whatever tomorrow brings, I hope you can carry a little of this peaceful moment with you.",
  animal_final_breath: "Your little friend is resting. Let’s take one last gentle breath together.",
  animal_awakes: "Oh, look at that… Your breath gave life to your little friend!",
  animal_wonder: "You created something wonderful, just by taking a moment to breathe."
});
export function inspectWav(bytes, maxSeconds = 45) {
  if (!Buffer.isBuffer(bytes) || bytes.length < 44 || bytes.length > 4 * 1024 * 1024 || bytes.toString('ascii', 0, 4) !== 'RIFF' ||
      bytes.toString('ascii', 8, 12) !== 'WAVE') throw new Error('INVALID_AUDIO');
  let format, data;
  for (let pos = 12; pos + 8 <= bytes.length;) {
    const kind = bytes.toString('ascii', pos, pos + 4), size = bytes.readUInt32LE(pos + 4), start = pos + 8;
    if (start + size > bytes.length) throw new Error('INVALID_AUDIO');
    if (kind === 'fmt ' && size >= 16) format = { encoding: bytes.readUInt16LE(start), channels: bytes.readUInt16LE(start + 2), rate: bytes.readUInt32LE(start + 4), bits: bytes.readUInt16LE(start + 14) };
    if (kind === 'data') data = { start, size };
    pos = start + size + (size % 2);
  }
  if (!format || !data || format.encoding !== 1 || format.bits !== 16 || ![1,2].includes(format.channels) || ![16000,24000,44100,48000].includes(format.rate)) throw new Error('INVALID_AUDIO');
  const duration = data.size / (format.channels * 2 * format.rate);
  if (duration < .1 || duration > maxSeconds || data.size % (format.channels * 2)) throw new Error('INVALID_AUDIO_DURATION');
  return { ...format, duration, ...data };
}
export function createSpeechService({ openaiKey, fetchFn, speechDir, reflection, library, ttsModel = 'gpt-4o-mini-tts', sttModel = 'gpt-transcribe', voice = 'marin', enabled = true, clock = Date.now }) {
  const ai = openaiTransport({ key: openaiKey, fetchFn });
  const entries = new Map(), pending = new Map();
  const clean = () => { for (const [id, e] of entries) if (e.private && e.expires < clock()) entries.delete(id); };
  async function narrate(cue, planId, signal) {
    clean();
    if (!Object.hasOwn(narrationCues, cue)) throw new Error('INVALID_NARRATION_CUE');
    if (!enabled || !ai.configured) throw new Error('VOICE_NOT_CONFIGURED');
    const privateText = planId ? reflection.narration(planId, cue) : null;
    if (planId && !reflection.get(planId)) throw new Error('REFLECTION_PLAN_EXPIRED');
    const text = privateText || narrationCues[cue];
    const personal = !!privateText;
    const id = createHash('sha256').update(JSON.stringify({ text, ttsModel, voice, scope: personal ? planId : 'scripted', version: 1 })).digest('hex');
    if (entries.has(id)) return { id, text, cached: true, duration: entries.get(id).duration };
    if (pending.has(id)) return pending.get(id);
    if (pending.size >= 2 || entries.size >= 64) throw new Error('VOICE_BUSY');
    const work = (async () => {
      let bytes, cached = false;
      const file = path.join(speechDir, id + '.wav');
      if (!personal) { try { bytes = await readFile(file); inspectWav(bytes); cached = true; } catch (e) { if (e.code !== 'ENOENT') throw new Error('AUDIO_CACHE_INVALID'); } }
      if (!bytes) {
        bytes = await ai.request('audio/speech', { model: ttsModel, voice, input: text, response_format: 'wav', speed: .92,
          instructions: cue.startsWith('animal_')
            ? 'Speak as a warm rabbit companion with a gentle smile. Let quiet delight come through in the awakening, and end with sincere, calm encouragement. Use natural conversational pacing and read only the supplied words.'
            : 'Speak as a warm, gentle companion. Calm, natural conversational pacing. Clear English. No dramatic whispering, sound effects, music or long pauses. Read only the supplied words.' }, signal, true);
        inspectWav(bytes);
        if (!personal) {
          await library.ensureSpace(bytes.length + 1024); await mkdir(speechDir, { recursive: true });
          await writeFile(file + '.tmp', bytes, { mode: 0o600 }); await rename(file + '.tmp', file);
        }
      }
      const { duration } = inspectWav(bytes);
      if (personal && !reflection.get(planId)) throw new Error('REFLECTION_PLAN_EXPIRED');
      entries.set(id, { bytes, duration, private: personal, planId: personal ? planId : '', expires: clock() + 5 * 60000 });
      if (personal) { const timer = setTimeout(() => entries.delete(id), 5 * 60000); timer.unref(); }
      return { id, text, cached, duration };
    })();
    pending.set(id, work); try { return await work; } finally { pending.delete(id); }
  }
  function audio(id) { clean(); if (!/^[a-f0-9]{64}$/.test(id)) return null; const item = entries.get(id); if (item?.private && !reflection.get(item.planId)) { entries.delete(id); return null; } return item?.bytes ?? null; }
  async function transcribe(bytes, signal) {
    if (!enabled || !ai.configured) throw new Error('VOICE_NOT_CONFIGURED');
    const wav = inspectWav(bytes, 20);
    let energy = 0; for (let i = wav.start; i < wav.start + wav.size; i += 2) energy += Math.abs(bytes.readInt16LE(i));
    if (energy / (wav.size / 2) < 10) throw new Error('NO_SPEECH_DETECTED');
    const form = new FormData(); form.set('model', sttModel); form.set('file', new Blob([bytes], { type: 'audio/wav' }), 'speech.wav');
    const result = await ai.request('audio/transcriptions', form, signal);
    if (typeof result.text !== 'string' || !result.text.trim() || /[<>\x00-\x1f]/.test(result.text)) throw new Error('INVALID_TRANSCRIPTION');
    return { text: result.text.trim().slice(0, 512), truncated: result.text.trim().length > 512 };
  }
  return { narrate, audio, transcribe, cancel: planId => { for (const [id, e] of entries) if (e.planId === planId) entries.delete(id); }, configured: enabled && ai.configured };
}
