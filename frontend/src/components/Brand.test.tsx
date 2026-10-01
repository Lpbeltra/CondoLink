import { render, screen } from '@testing-library/react'
import { createTheme, ThemeProvider } from '@mui/material/styles'
import { describe, expect, it } from 'vitest'
import { Brand } from './Brand'

describe('Brand', () => {
  it('uses approved light and dark lockups', () => {
    const { rerender } = render(<Brand />)
    expect(screen.getByRole('img', { name: 'Comvy' })).toHaveAttribute('src', '/comvy-logo.svg')

    rerender(<Brand surface="dark" />)
    expect(screen.getByRole('img', { name: 'Comvy' })).toHaveAttribute('src', '/comvy-logo-dark.svg')
  })

  it('uses the approved symbol alone in compact headers', () => {
    render(<Brand compact />)
    expect(screen.getByRole('img', { name: 'Comvy' })).toHaveAttribute('src', '/comvy-symbol.svg')
  })

  it('selects the reverse lockup automatically in dark theme', () => {
    render(<ThemeProvider theme={createTheme({ palette: { mode: 'dark' } })}><Brand /></ThemeProvider>)
    expect(screen.getByRole('img', { name: 'Comvy' })).toHaveAttribute('src', '/comvy-logo-dark.svg')
  })
})
