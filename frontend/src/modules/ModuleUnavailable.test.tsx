import { render, screen } from '@testing-library/react'
import { ThemeProvider } from '@mui/material/styles'
import { describe, expect, it } from 'vitest'
import { createAppTheme } from '../theme/createAppTheme'
import { ModuleUnavailable } from './ModuleUnavailable'

describe('ModuleUnavailable', () => {
  it('shows the safe unavailable message without commercial language', () => {
    render(<ThemeProvider theme={createAppTheme('light')}><ModuleUnavailable /></ThemeProvider>)
    expect(screen.getByText('Este recurso não está disponível para este condomínio.')).toBeInTheDocument()
    expect(screen.queryByText(/plano|preço/i)).not.toBeInTheDocument()
  })
})
