import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { AppThemeProvider } from '../theme/AppThemeProvider'
import { AuthShell } from './AuthShell'

describe('AuthShell', () => {
  it('renders the accessible brand, theme control and page content', () => {
    render(
      <AppThemeProvider>
        <AuthShell><h1>Conteúdo de autenticação</h1></AuthShell>
      </AppThemeProvider>,
    )

    expect(screen.getByRole('img', { name: 'Comvy' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Alternar para o tema/i })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Conteúdo de autenticação' })).toBeInTheDocument()
  })
})
