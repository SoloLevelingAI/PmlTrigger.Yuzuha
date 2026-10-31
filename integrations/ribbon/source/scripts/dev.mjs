import { spawn } from 'node:child_process';
const children = [
  spawn(process.execPath, ['scripts/start-backend.mjs'], { stdio: 'inherit' }),
  spawn(process.execPath, ['node_modules/vite/bin/vite.js', '--config', 'apps/web/vite.config.ts'], { stdio: 'inherit' })
];
function stop() { for (const child of children) child.kill(); }
for (const signal of ['SIGINT', 'SIGTERM']) process.on(signal, () => { stop(); process.exit(); });
for (const child of children) child.on('exit', code => { stop(); process.exit(code ?? 0); });
