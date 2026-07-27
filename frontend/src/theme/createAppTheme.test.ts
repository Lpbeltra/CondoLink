import { describe, expect, it } from 'vitest'
import { createAppTheme } from './createAppTheme'
import { palettes } from './tokens'

describe('createAppTheme', () => {
  it('builds a palette matching the requested mode', () => {
    expect(createAppTheme('light').palette.mode).toBe('light')
    expect(createAppTheme('dark').palette.mode).toBe('dark')
  })

  it('wires the semantic tokens into the MUI palette', () => {
    for (const mode of ['light', 'dark'] as const) {
      const theme = createAppTheme(mode)
      const tokens = palettes[mode]
      expect(theme.palette.primary.main).toBe(tokens.primary)
      expect(theme.palette.background.default).toBe(tokens.background)
      expect(theme.palette.background.paper).toBe(tokens.surface)
      expect(theme.palette.text.primary).toBe(tokens.textPrimary)
      expect(theme.palette.text.secondary).toBe(tokens.textSecondary)
      expect(theme.palette.divider).toBe(tokens.divider)
    }
  })

  it('produces genuinely different surfaces per mode', () => {
    const light = createAppTheme('light')
    const dark = createAppTheme('dark')
    expect(dark.palette.background.default).not.toBe(light.palette.background.default)
    expect(dark.palette.background.paper).not.toBe(light.palette.background.paper)
    expect(dark.palette.text.primary).not.toBe(light.palette.text.primary)
  })

  it('declares colorScheme so native controls follow the theme', () => {
    const baseline = createAppTheme('dark').components?.MuiCssBaseline?.styleOverrides as
      Record<string, Record<string, unknown>> | undefined
    expect(baseline?.[':root']?.colorScheme).toBe('dark')
  })

  it('honours prefers-reduced-motion globally', () => {
    const baseline = createAppTheme('light').components?.MuiCssBaseline?.styleOverrides as
      Record<string, unknown> | undefined
    expect(baseline).toHaveProperty('@media (prefers-reduced-motion: reduce)')
  })

  it('keeps dark shadows opaque enough to read on dark surfaces', () => {
    const dark = createAppTheme('dark')
    // MUI index 1 is the first real elevation step.
    expect(dark.shadows[1]).toContain('rgba(0,0,0')
  })

  it('keeps the 44px minimum touch target on buttons', () => {
    const button = createAppTheme('light').components?.MuiButton?.styleOverrides?.root as
      Record<string, unknown>
    expect(button.minHeight).toBe(44)
  })

  it('removes the MUI dark-mode paper gradient that fights the surface tokens', () => {
    const paper = createAppTheme('dark').components?.MuiPaper?.styleOverrides?.root as
      Record<string, unknown>
    expect(paper.backgroundImage).toBe('none')
  })

  it('uses the mode-specific scrim for modal backdrops', () => {
    for (const mode of ['light', 'dark'] as const) {
      const backdrop = createAppTheme(mode).components?.MuiBackdrop?.styleOverrides?.root as
        Record<string, unknown>
      expect(backdrop.backgroundColor).toBe(palettes[mode].scrim)
    }
  })
})
