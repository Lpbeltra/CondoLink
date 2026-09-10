export const pwaManifest = {
  id: '/',
  name: 'Comvy',
  short_name: 'Comvy',
  description: 'Comunicação clara entre moradores, síndicos e administradoras.',
  theme_color: '#6682f4',
  background_color: '#f6f8fc',
  display: 'standalone' as const,
  start_url: '/',
  scope: '/',
  lang: 'pt-BR',
  icons: [
    { src: '/comvy-icon-192-v1.png', sizes: '192x192', type: 'image/png', purpose: 'any' as const },
    { src: '/comvy-icon-512-v1.png', sizes: '512x512', type: 'image/png', purpose: 'any' as const },
    { src: '/comvy-maskable-512-v1.png', sizes: '512x512', type: 'image/png', purpose: 'maskable' as const },
    { src: '/icon.svg', sizes: 'any', type: 'image/svg+xml', purpose: 'any' as const },
  ],
}
