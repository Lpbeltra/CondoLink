import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

const auth = vi.hoisted(() => ({ user: null as null | { id: string }, login: vi.fn() }))

vi.mock('../auth/AuthContext', () => ({ useAuth: () => auth }))
vi.mock('../theme/ThemeModeToggle', () => ({
  ThemeModeToggle: () => <button aria-label="Alternar para o tema escuro" />,
}))

import { LoginPage } from './LoginPage'

describe('LoginPage', () => {
  afterEach(cleanup)
  beforeEach(() => {
    auth.user = null
    auth.login.mockReset()
  })

  function renderPage() {
    render(
      <MemoryRouter initialEntries={['/login']}>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/change-password" element={<h1>Troca obrigatória</h1>} />
          <Route path="/forgot-password" element={<h1>Recuperar senha</h1>} />
          <Route path="/" element={<h1>Início</h1>} />
        </Routes>
      </MemoryRouter>,
    )
  }

  it('keeps accessible fields and toggles password visibility', async () => {
    renderPage()
    const password = screen.getByLabelText(/^Senha/)
    expect(screen.getByLabelText(/^E-mail/)).toHaveAttribute('autocomplete', 'email')
    expect(password).toHaveAttribute('autocomplete', 'current-password')
    expect(password).toHaveAttribute('type', 'password')

    await userEvent.click(screen.getByRole('button', { name: 'Exibir senha' }))
    expect(password).toHaveAttribute('type', 'text')
  })

  it('links to password recovery without changing the login flow', async () => {
    renderPage()
    await userEvent.click(screen.getByRole('link', { name: 'Esqueci minha senha' }))
    expect(screen.getByRole('heading', { name: 'Recuperar senha' })).toBeInTheDocument()
  })

  it('shows loading then preserves the normal redirect after login', async () => {
    let resolveLogin: (value: { requiresPasswordChange: false }) => void = () => undefined
    auth.login.mockReturnValue(new Promise(resolve => { resolveLogin = resolve }))
    renderPage()
    fireEvent.change(screen.getByLabelText(/^E-mail/), { target: { value: 'pessoa@example.com' } })
    fireEvent.change(screen.getByLabelText(/^Senha/), { target: { value: 'Senha1A' } })
    fireEvent.click(screen.getByRole('button', { name: 'Entrar' }))

    expect(await screen.findByRole('button', { name: 'Entrando…' })).toBeDisabled()
    resolveLogin({ requiresPasswordChange: false })
    expect(await screen.findByRole('heading', { name: 'Início' })).toBeInTheDocument()
  })

  it('shows the current authentication error and preserves the temporary-password redirect', async () => {
    auth.login.mockRejectedValueOnce(new Error('offline'))
    renderPage()
    fireEvent.change(screen.getByLabelText(/^E-mail/), { target: { value: 'pessoa@example.com' } })
    fireEvent.change(screen.getByLabelText(/^Senha/), { target: { value: 'Senha1A' } })
    fireEvent.click(screen.getByRole('button', { name: 'Entrar' }))
    expect(await screen.findByText(/Não foi possível concluir o acesso agora/i)).toBeInTheDocument()

    auth.login.mockResolvedValueOnce({ requiresPasswordChange: true, email: 'pessoa@example.com', temporaryPassword: 'Senha1A' })
    fireEvent.click(screen.getByRole('button', { name: 'Entrar' }))
    await waitFor(() => expect(screen.getByRole('heading', { name: 'Troca obrigatória' })).toBeInTheDocument())
  })
})
