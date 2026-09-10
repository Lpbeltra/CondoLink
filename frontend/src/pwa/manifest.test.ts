import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, expect, it } from 'vitest'
import { pwaManifest } from './manifest'

const publicAsset = (name: string) => resolve(process.cwd(), 'public', name)

describe('PWA manifest identity', () => {
  it('defines stable identity and install icons', () => {
    expect(pwaManifest.id).toBe('/')
    expect(pwaManifest.start_url).toBe('/')
    expect(pwaManifest.scope).toBe('/')
    expect(pwaManifest.icons).toEqual(expect.arrayContaining([
      expect.objectContaining({ src: '/comvy-icon-192-v1.png', sizes: '192x192', purpose: 'any' }),
      expect.objectContaining({ src: '/comvy-icon-512-v1.png', sizes: '512x512', purpose: 'any' }),
      expect.objectContaining({ src: '/comvy-maskable-512-v1.png', sizes: '512x512', purpose: 'maskable' }),
    ]))
  })

  it.each([
    ['comvy-icon-192-v1.png', 192],
    ['comvy-icon-512-v1.png', 512],
    ['comvy-maskable-512-v1.png', 512],
    ['apple-touch-icon-180-v1.png', 180],
  ])('ships %s at %ix%i', (file, expectedSize) => {
    const png = readFileSync(publicAsset(file))
    expect(png.subarray(1, 4).toString()).toBe('PNG')
    expect(png.readUInt32BE(16)).toBe(expectedSize)
    expect(png.readUInt32BE(20)).toBe(expectedSize)
  })

  it('uses the dedicated Apple touch icon and standalone metadata', () => {
    const html = readFileSync(resolve(process.cwd(), 'index.html'), 'utf8')
    expect(html).toContain('href="/apple-touch-icon-180-v1.png"')
    expect(html).toContain('name="apple-mobile-web-app-capable" content="yes"')
    expect(html).toContain('name="apple-mobile-web-app-status-bar-style" content="default"')
    expect(html).toContain('name="apple-mobile-web-app-title" content="Comvy"')
  })
})
