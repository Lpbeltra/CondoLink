import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

const { post } = vi.hoisted(() => ({ post: vi.fn() }))
vi.mock('../services/api', () => ({ api: { post } }))
vi.mock('../theme/ThemeModeToggle', () => ({ ThemeModeToggle: () => null }))

import { ForgotPasswordPage } from './ForgotPasswordPage'

describe('ForgotPasswordPage', () => {
  afterEach(cleanup)
  beforeEach(() => post.mockReset())

  function renderPage() {
    render(
      <MemoryRouter initialEntries={['/forgot-password']}>
        <Routes>
          <Route path="/forgot-password" element={<ForgotPasswordPage />} />
          <Route path="/login" element={<h1>Login</h1>} />
        </Routes>
      </MemoryRouter>,
    )
  }

  it('submits a valid email and always presents the neutral confirmation', async () => {
    post.mockResolvedValueOnce({ data: {} })
    renderPage()
    const user = userEvent.setup()
    const email = screen.getByLabelText(/^E-mail/)
    expect(email).toHaveAttribute('type', 'email')
    expect(email).toHaveAttribute('autocomplete', 'email')
    await user.type(email, 'inexistente@example.com')
    await user.click(screen.getByRole('button', { name: 'Enviar instruções' }))

    expect(post).toHaveBeenCalledWith('/auth/forgot-password', { email: 'inexistente@example.com' })
    expect(await screen.findByRole('heading', { name: 'Verifique seu e-mail' })).toBeInTheDocument()
    expect(screen.getByText(/Se existir uma conta elegível/i)).toBeInTheDocument()
  })

  it('shows loading and allows retry after a transport error', async () => {
    let rejectRequest: (reason?: unknown) => void = () => undefined
    post.mockReturnValueOnce(new Promise((_, reject) => { rejectRequest = reject }))
    renderPage()
    fireEvent.change(screen.getByLabelText(/^E-mail/), { target: { value: 'pessoa@example.com' } })
    fireEvent.click(screen.getByRole('button', { name: 'Enviar instruções' }))
    expect(await screen.findByRole('button', { name: 'Enviando…' })).toBeDisabled()
    rejectRequest(new Error('offline'))
    expect(await screen.findByText('Não foi possível processar sua solicitação agora. Tente novamente.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Enviar instruções' })).toBeEnabled()
  })

  it('returns to login', async () => {
    renderPage()
    await userEvent.click(screen.getByRole('link', { name: 'Voltar para o login' }))
    expect(screen.getByRole('heading', { name: 'Login' })).toBeInTheDocument()
  })
})
