// Bounded server-side OpenAI transport. Never logs provider bodies, keys or private input.
export function openaiTransport({ key, fetchFn = fetch, timeoutMs = 45000 }) {
  const configured = !!key && !/^(replace_|your_)/.test(key);
  async function request(route, body, signal, binary = false) {
    if (!configured) throw new Error('OPENAI_NOT_CONFIGURED');
    const multipart = body instanceof FormData;
    const response = await fetchFn('https://api.openai.com/v1/' + route, {
      method: 'POST', redirect: 'error', headers: { Authorization: 'Bearer ' + key, ...(!multipart ? { 'Content-Type': 'application/json' } : {}) },
      body: multipart ? body : JSON.stringify(body), signal: signal ? AbortSignal.any([signal, AbortSignal.timeout(timeoutMs)]) : AbortSignal.timeout(timeoutMs)
    });
    if (!response.ok) throw new Error(response.status === 401 ? 'OPENAI_AUTH_FAILED' : 'OPENAI_UNAVAILABLE');
    const limit = binary ? 4 * 1024 * 1024 : 65536, chunks = []; let size = 0;
    for await (const part of response.body) { size += part.length; if (size > limit) throw new Error('OPENAI_RESPONSE_TOO_LARGE'); chunks.push(part); }
    const bytes = Buffer.concat(chunks); return binary ? bytes : JSON.parse(bytes.toString('utf8'));
  }
  async function structured(model, instructions, input, name, schema, signal) {
    const result = await request('responses', { model, store: false, max_output_tokens: 1400, instructions, input,
      text: { format: { type: 'json_schema', name, strict: true, schema } } }, signal);
    if (result.status !== 'completed') throw new Error('INVALID_REFLECTION_RESPONSE');
    const text = result.output?.flatMap(x => x.content ?? []).filter(x => x.type === 'output_text').map(x => x.text).join('');
    return JSON.parse(text);
  }
  return { configured, request, structured };
}
