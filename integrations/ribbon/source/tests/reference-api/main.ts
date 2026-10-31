import { createApiServer } from './server.ts';
const port = Number(process.env.UIC_PORT ?? 4318);
const server = createApiServer({ serveWeb: process.argv.includes('--serve-web'), dataDirectory: process.env.UIC_DATA_DIR });
server.listen(port, '127.0.0.1', () => console.log(`Ribbon Studio API ready: http://127.0.0.1:${port}`));
server.on('error', error => { console.error(error.message); process.exit(1); });
for (const signal of ['SIGINT', 'SIGTERM'] as const) process.on(signal, () => {
  server.closeAllConnections();
  server.close(() => process.exit(0));
});
