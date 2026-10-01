import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, expect, it } from 'vitest'
import { pwaManifest } from './manifest'

const publicAsset = (name: string) => resolve(process.cwd(), 'public', name)

describe('PWA brand identity', () => {
  it('uses the approved symbol and brand blue', () => {
    expect(pwaManifest.id).toBe('/')
    expect(pwaManifest.start_url).toBe('/app')
    expect(pwaManifest.scope).toBe('/')
    expect(pwaManifest.theme_color).toBe('#2563eb')
    expect(pwaManifest.icons).toEqual(expect.arrayContaining([
      expect.objectContaining({ src: '/comvy-icon-192-v1.png', sizes: '192x192', purpose: 'any' }),
      expect.objectContaining({ src: '/comvy-icon-512-v1.png', sizes: '512x512', purpose: 'any' }),
      expect.objectContaining({ src: '/comvy-maskable-512-v1.png', sizes: '512x512', purpose: 'maskable' }),
      expect.objectContaining({ src: '/comvy-symbol.svg', sizes: 'any', purpose: 'any' }),
    ]))
  })

  it('ships the approved vector wherever an SVG brand icon is exposed', () => {
    const symbol = readFileSync(publicAsset('comvy-symbol.svg'))
    expect(readFileSync(publicAsset('icon.svg'))).toEqual(symbol)
    expect(readFileSync(publicAsset('icon-maskable.svg'))).toEqual(symbol)
  })

  it.each([
    ['comvy-icon-192-v1.png', 192],
    ['comvy-icon-512-v1.png', 512],
    ['comvy-maskable-512-v1.png', 512],
    ['apple-touch-icon-180-v1.png', 180],
  ])('renders %s at %ix%i from the approved symbol', (file, expectedSize) => {
    const png = readFileSync(publicAsset(file))
    expect(png.subarray(1, 4).toString()).toBe('PNG')
    expect(png.readUInt32BE(16)).toBe(expectedSize)
    expect(png.readUInt32BE(20)).toBe(expectedSize)
    expect(png[24]).toBe(8)
  })

  it('uses the symbol favicon and existing Apple app-icon size', () => {
    const html = readFileSync(resolve(process.cwd(), 'index.html'), 'utf8')
    expect(html).toContain('href="/comvy-symbol.svg" type="image/svg+xml"')
    expect(html).toContain('href="/apple-touch-icon-180-v1.png"')
    expect(html).toContain('name="theme-color" content="#2563eb"')
    expect(html).toContain('name="apple-mobile-web-app-capable" content="yes"')
    expect(html).toContain('name="apple-mobile-web-app-status-bar-style" content="default"')
    expect(html).toContain('name="apple-mobile-web-app-title" content="Comvy"')
  })
})
