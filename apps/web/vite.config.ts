import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import path from 'node:path';

// The contract's server is `/api/v1` — same origin. Proxying in development keeps the
// browser on one origin, so a preflight is never refused by CORS before the API answers.
export default defineConfig({
  plugins: [react()],
  // The prototype's ui/ components import from '@/lib/utils'; the alias is part of that stack.
  resolve: { alias: { '@': path.resolve(__dirname, './src') } },
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: process.env.MARITES_API_URL ?? 'http://127.0.0.1:8080',
        changeOrigin: false,
      },
    },
  },
});
