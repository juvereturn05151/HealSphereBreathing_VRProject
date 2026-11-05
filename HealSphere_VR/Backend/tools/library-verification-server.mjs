// Uses the real saved library. The verification service can never submit a paid task.
import { writeFile, readFile } from 'node:fs/promises';
import { configuration } from '../src/config.js';
import { createApp } from '../src/app.js';
const options = configuration();
const record = JSON.parse(await readFile(new URL('../data/authorized-fox-35da0db2-942b-4760-b312-8629110f8f02.json', import.meta.url), 'utf8'));
if (record.status !== 'ready' || record.paidPosts !== 1 || !record.creation_id) throw new Error('APPROVED_REAL_FOX_REQUIRED');
const { app, library } = await createApp({ ...options, enabled: false, fetchFn: (url, init) => {
  if (String(url).startsWith('https://openapi.tripo3d.ai/') && init?.method === 'POST') throw new Error('PAID_REQUEST_FORBIDDEN_DURING_REUSE_VERIFICATION');
  return fetch(url, init);
} });
const server = app.listen(3337, '127.0.0.1', async error => {
  if (error) { console.error('Verification service could not bind:', error.code); library.close(); process.exitCode = 1; return; }
  await writeFile(new URL('../../Library/HealSphereLibraryVerification.connection.json', import.meta.url), JSON.stringify({
    url: 'http://127.0.0.1:3337', token: options.token, creationId: record.creation_id, expectedPaidCount: 1
  }), { mode: 0o600 });
  console.log('Saved-library verification server ready on localhost:3337. Paid Tripo operations disabled.');
});
process.on('SIGINT', () => server.close(() => { library.close(); process.exit(0); }));
