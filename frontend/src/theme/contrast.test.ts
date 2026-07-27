import { describe, expect, it } from 'vitest'
import { palettes, type ColorTokens, type ThemeMode } from './tokens'

/** Relative luminance per WCAG 2.1. */
function luminance(hex: string): number {
  const value = hex.replace('#', '')
  const channels = [0, 2, 4].map((offset) => {
    const channel = parseInt(value.slice(offset, offset + 2), 16) / 255
    return channel <= 0.03928
      ? channel / 12.92
      : Math.pow((channel + 0.055) / 1.055, 2.4)
  })
  return 0.2126 * channels[0] + 0.7152 * channels[1] + 0.0722 * channels[2]
}

function contrastRatio(foreground: string, background: string): number {
  const a = luminance(foreground)
  const b = luminance(background)
  return (Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05)
}

/** Body-text pairs must clear AA (4.5:1) in both modes. */
const bodyTextPairs: ReadonlyArray<[keyof ColorTokens, keyof ColorTokens]> = [
  ['textPrimary', 'background'],
  ['textPrimary', 'surface'],
  ['textPrimary', 'surfaceMuted'],
  ['textSecondary', 'background'],
  ['textSecondary', 'surface'],
  ['textSecondary', 'surfaceMuted'],
  ['error', 'surface'],
  ['success', 'surface'],
  ['warning', 'surface'],
  ['info', 'surface'],
]

const modes: ThemeMode[] = ['light', 'dark']

describe.each(modes)('%s palette contrast', (mode) => {
  const tokens = palettes[mode]

  it.each(bodyTextPairs)('%s on %s meets WCAG AA (4.5:1)', (fg, bg) => {
    const ratio = contrastRatio(tokens[fg] as string, tokens[bg] as string)
    expect(
      ratio,
      `${mode}: ${fg} (${tokens[fg]}) on ${bg} (${tokens[bg]}) = ${ratio.toFixed(2)}:1`,
    ).toBeGreaterThanOrEqual(4.5)
  })

  it('primary is legible as a UI element against surfaces (3:1)', () => {
    expect(contrastRatio(tokens.primary, tokens.surface)).toBeGreaterThanOrEqual(3)
    expect(contrastRatio(tokens.primary, tokens.background)).toBeGreaterThanOrEqual(3)
  })

  it('text on a primary-filled button meets AA', () => {
    expect(
      contrastRatio(tokens.primaryContrast, tokens.primary),
    ).toBeGreaterThanOrEqual(4.5)
  })

  it('separates background from surface so elevation is perceivable', () => {
    expect(tokens.background).not.toBe(tokens.surface)
  })
})

describe('palette parity between modes', () => {
  it('defines exactly the same token keys in both modes', () => {
    expect(Object.keys(palettes.dark).sort()).toEqual(Object.keys(palettes.light).sort())
  })

  it('does not reuse light surfaces in dark mode', () => {
    expect(palettes.dark.background).not.toBe(palettes.light.background)
    expect(palettes.dark.surface).not.toBe(palettes.light.surface)
    expect(palettes.dark.textPrimary).not.toBe(palettes.light.textPrimary)
  })

  it('inverts the surface/text relationship in dark mode', () => {
    // Light: dark text on light surface. Dark: light text on dark surface.
    expect(luminance(palettes.light.textPrimary))
      .toBeLessThan(luminance(palettes.light.surface))
    expect(luminance(palettes.dark.textPrimary))
      .toBeGreaterThan(luminance(palettes.dark.surface))
  })
})
