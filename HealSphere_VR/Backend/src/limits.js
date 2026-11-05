import { MAX_BYTES } from './glb.js';
export function generationSettings(input = {}) {
  const integer = (name, fallback, low, high) => {
    const n = input[name] ?? fallback;
    if (!Number.isSafeInteger(n) || n < low || n > high) throw new Error('INVALID_GENERATION_LIMIT_' + name);
    return n;
  };
  const providers = input.allowedProviders ?? ['tripo'];
  if (!Array.isArray(providers) || providers.some(p => p !== 'tripo')) throw new Error('INVALID_ALLOWED_PROVIDERS');
  // -1 explicitly disables a paid-count quota; zero still blocks all new reservations.
  return { maxConcurrent: integer('maxConcurrent', 1, 1, 4), perSession: integer('perSession', 1, -1, 10),
    maxLifetime: integer('maxLifetime', 1, -1, 10000), requestsPerMinute: integer('requestsPerMinute', 60, 1, 1000),
    maxModelBytes: integer('maxModelBytes', MAX_BYTES, 1024, MAX_BYTES),
    maxStorageBytes: integer('maxStorageBytes', 512 * 1024 * 1024, MAX_BYTES * 2, 10 * 1024 * 1024 * 1024),
    allowedProviders: providers, sharedCollection: input.sharedCollection === true, authorizedPrompt: input.authorizedPrompt };
}
