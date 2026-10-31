import { defineConfig } from 'vite';
import { fileURLToPath } from 'node:url';
export default defineConfig({
  root: fileURLToPath(new URL('./', import.meta.url)),
  server: { host: '127.0.0.1', port: 4317, strictPort: true, proxy: { '/api': 'http://127.0.0.1:4318' } },
  build: { outDir: 'dist', emptyOutDir: true }
});
