import { render, screen } from '@testing-library/react'
import { Button, Paper, ThemeProvider, Typography } from '@mui/material'
import { describe, expect, it } from 'vitest'
import { createAppTheme } from './createAppTheme'
import { palettes, type ThemeMode } from './tokens'

/** Normalises a CSS colour to lowercase hex-ish form for comparison. */
function rgbToHex(color: string): string {
  const match = color.match(/rgba?\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)/)
  if (!match) return color.toLowerCase()
  return `#${[1, 2, 3]
    .map((index) => Number(match[index]).toString(16).padStart(2, '0'))
    .join('')}`
}

function renderInMode(mode: ThemeMode) {
  return render(
    <ThemeProvider theme={createAppTheme(mode)}>
      <Paper data-testid="surface">
        <Typography data-testid="body">Solicitações do condomínio</Typography>
        <Typography data-testid="muted" color="text.secondary">
          Atualizado agora
        </Typography>
        <Button data-testid="cta" variant="contained">
          Nova solicitação
        </Button>
      </Paper>
    </ThemeProvider>,
  )
}

describe.each(['light', 'dark'] as ThemeMode[])(
  'components rendered in %s mode',
  (mode) => {
    const tokens = palettes[mode]

    it('paints raised surfaces with the surface token', () => {
      renderInMode(mode)
      const surface = getComputedStyle(screen.getByTestId('surface')).backgroundColor
      expect(rgbToHex(surface)).toBe(tokens.surface.toLowerCase())
    })

    it('paints body text with the primary text token', () => {
      renderInMode(mode)
      const body = getComputedStyle(screen.getByTestId('body')).color
      expect(rgbToHex(body)).toBe(tokens.textPrimary.toLowerCase())
    })

    it('paints secondary text with the secondary token', () => {
      renderInMode(mode)
      const muted = getComputedStyle(screen.getByTestId('muted')).color
      expect(rgbToHex(muted)).toBe(tokens.textSecondary.toLowerCase())
    })

    it('keeps the primary action at a 44px minimum touch height', () => {
      renderInMode(mode)
      expect(getComputedStyle(screen.getByTestId('cta')).minHeight).toBe('44px')
    })
  },
)

describe('light vs dark rendering actually differs', () => {
  it('renders different surface and text colours for the same markup', () => {
    const light = renderInMode('light')
    const lightSurface = getComputedStyle(
      light.getByTestId('surface') as HTMLElement,
    ).backgroundColor
    const lightText = getComputedStyle(light.getByTestId('body') as HTMLElement).color
    light.unmount()

    const dark = renderInMode('dark')
    const darkSurface = getComputedStyle(
      dark.getByTestId('surface') as HTMLElement,
    ).backgroundColor
    const darkText = getComputedStyle(dark.getByTestId('body') as HTMLElement).color

    expect(darkSurface).not.toBe(lightSurface)
    expect(darkText).not.toBe(lightText)
  })
})
