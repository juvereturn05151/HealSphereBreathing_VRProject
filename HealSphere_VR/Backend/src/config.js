import dotenv from 'dotenv';
import path from 'node:path';
import { existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
export const backendRoot = fileURLToPath(new URL('../', import.meta.url));
export function configuration() {
  dotenv.config({ path: path.join(backendRoot, '.env'), quiet: true });
  const e = process.env;
  const count = (key, fallback) => e[key] === undefined ? fallback : Number(e[key]);
  const demo = e.DEMO_GLB_PATH ? path.resolve(backendRoot, e.DEMO_GLB_PATH) : path.resolve(backendRoot, '../Assets/StreamingAssets/healsphere-cat-demo.glb');
  return { key: e.TRIPO_API_KEY, token: e.BACKEND_ACCESS_TOKEN, enabled: e.ENABLE_GENERATION === 'true',
    openaiKey: e.OPENAI_API_KEY, openaiModel: e.OPENAI_MODEL || 'gpt-4.1-mini', offlineConversation: e.ALLOW_OFFLINE_CONVERSATION !== 'false',
    reviewMode: e.MODEL_REVIEW_MODE || 'openai', demoModelPath: existsSync(demo) ? demo : undefined,
    reflectionDemoDir: path.resolve(backendRoot, '../Assets/StreamingAssets/ReflectionDemo'),
    speechModel: e.OPENAI_TTS_MODEL || 'gpt-4o-mini-tts', transcriptionModel: e.OPENAI_STT_MODEL || 'gpt-transcribe',
    speechVoice: e.OPENAI_TTS_VOICE || 'marin', speechEnabled: e.OPENAI_SPEECH_ENABLED !== 'false',
    taskTimeoutMs: count('GENERATION_TIMEOUT_SECONDS', 900) * 1000,
    dataDir: path.join(backendRoot, 'data'), hosts: (e.MODEL_DOWNLOAD_HOSTS || 'tripo3d.ai,openapi-dl.data.tripo3d.com').split(',').map(x => x.trim()).filter(Boolean),
    generationLimits: { maxConcurrent: count('MAX_SIMULTANEOUS_GENERATIONS', 1), perSession: count('MAX_GENERATIONS_PER_SESSION', 1),
      maxLifetime: count('MAX_LIFETIME_PAID_GENERATIONS', 1), requestsPerMinute: count('MAX_REQUESTS_PER_MINUTE', 60),
      maxModelBytes: count('MAX_MODEL_BYTES', 32 * 1024 * 1024), maxStorageBytes: count('MAX_RETAINED_STORAGE_BYTES', 512 * 1024 * 1024),
      allowedProviders: (e.ALLOWED_GENERATION_PROVIDERS ?? 'tripo').split(',').filter(Boolean), sharedCollection: e.SHARED_COLLECTION_ENABLED === 'true' } };
}
