import { createApp } from './app.js';
import { configuration } from './config.js';

try {
  const { app } = await createApp(configuration());
  const server = app.listen(Number(process.env.PORT || 3000), process.env.HOST || '127.0.0.1', () => {
    console.log('Tripo backend listening. Generation requires an explicit request.');
  });
  server.on('error', () => { console.error('Backend could not listen. Check HOST and PORT.'); process.exitCode = 1; });
} catch {
  console.error('Backend startup failed. Check local configuration and data journal; secrets are never printed.');
  process.exitCode = 1;
}
