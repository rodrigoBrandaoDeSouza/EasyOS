/// <reference types="vitest/config" />
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// Em desenvolvimento a API roda em http://localhost:5080. O proxy preserva o host acessado
// (ex.: cliente.localhost:5173), que é o que identifica o tenant no backend.
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    host: true,
    proxy: {
      '/api': { target: 'http://localhost:5080', changeOrigin: false },
    },
  },
  build: {
    outDir: '../backend/EasyOS.Api/wwwroot',
    emptyOutDir: true,
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
  },
})
