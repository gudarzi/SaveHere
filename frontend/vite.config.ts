import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from "tailwindcss";

export default defineConfig({
  plugins: [react()],
  build: {
    outDir: '../backend/wwwroot',
    emptyOutDir: true,
  },
  css: {
    postcss: {
      plugins: [tailwindcss()],
    },
  },
  server: {
    proxy: {
      '^/api': {
        target: "http://localhost:15112",
        secure: false
      },
      '^/swagger': {
        target: "http://localhost:15112",
        secure: false
      },
      '^/files': {
        target: "http://localhost:15112",
        secure: false
      },
      '^/filemanager': {
        target: "http://localhost:15112",
        secure: false
      },
      '^/ws': {
        target: "ws://localhost:15112",
        ws: true
      }
    },
    host: '0.0.0.0',
    port: 5173
  },
})
