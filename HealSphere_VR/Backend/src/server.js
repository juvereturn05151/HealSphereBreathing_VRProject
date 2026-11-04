import dotenv from 'dotenv';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createApp } from './app.js';

const root = fileURLToPath(new URL('../', import.meta.url));
dotenv.config({ path: path.join(root, '.env'), quiet: true });
try {
  const { app } = await createApp({
    key: process.env.TRIPO_API_KEY,
    token: process.env.BACKEND_ACCESS_TOKEN,
    enabled: process.env.ENABLE_GENERATION === 'true',
    dataDir: path.join(root, 'data'),
    hosts: (process.env.MODEL_DOWNLOAD_HOSTS || 'tripo3d.ai').split(',').map(x => x.trim()).filter(Boolean)
  });
  const server = app.listen(Number(process.env.PORT || 3000), process.env.HOST || '127.0.0.1', () => {
    console.log('Tripo backend listening. Generation requires an explicit request.');
  });
  server.on('error', () => { console.error('Backend could not listen. Check HOST and PORT.'); process.exitCode = 1; });
} catch {
  console.error('Backend startup failed. Check local configuration and data journal; secrets are never printed.');
  process.exitCode = 1;
}
