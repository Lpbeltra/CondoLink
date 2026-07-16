import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'
import { VitePWA } from 'vite-plugin-pwa'

export default defineConfig({
  plugins: [
    react(),
    VitePWA({
      registerType: 'autoUpdate',
      includeAssets: ['icon.svg'],
      manifest: {
        name: 'CondoLink',
        short_name: 'CondoLink',
        description: 'Seu condomínio mais conectado.',
        theme_color: '#1f5eff',
        background_color: '#f6f8fc',
        display: 'standalone',
        start_url: '/',
        scope: '/',
        lang: 'pt-BR',
        icons: [
          { src: '/icon.svg', sizes: 'any', type: 'image/svg+xml', purpose: 'any' },
          { src: '/icon-maskable.svg', sizes: 'any', type: 'image/svg+xml', purpose: 'maskable' },
        ],
      },
      workbox: {
        navigateFallback: '/index.html',
        runtimeCaching: [
          {
            urlPattern: ({ request }) => request.destination === 'font',
            handler: 'CacheFirst',
            options: { cacheName: 'condolink-fonts', expiration: { maxEntries: 10 } },
          },
        ],
      },
    }),
  ],
  server: {
    port: 5173,
    proxy: { '/api': { target: 'http://localhost:8080', changeOrigin: true, rewrite: (path) => path.replace(/^\/api/, '') } },
  },
  build: {
    chunkSizeWarningLimit: 550,
    rollupOptions: {
      output: {
        manualChunks(id) {
          if (!id.includes('node_modules')) return
          if (id.includes('@mui/icons-material')) return 'icons'
          if (id.includes('@mui') || id.includes('@emotion') || id.includes('react') || id.includes('scheduler')) return 'ui'
          if (id.includes('axios')) return 'http'
        },
      },
    },
  },
})
