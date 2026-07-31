import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'

const auth = vi.hoisted(() => ({
  user: null,
  isInitializing: false,
  login: vi.fn(),
  requestWhatsAppCode: vi.fn(),
  loginWithWhatsApp: vi.fn(),
  logout: vi.fn(),
}))

vi.mock('../auth/AuthContext', () => ({ useAuth: () => auth }))
vi.mock('../theme/ThemeModeToggle', () => ({
  ThemeModeToggle: () => null,
}))

import { LoginPage } from './LoginPage'

describe('WhatsApp passwordless login', () => {
  beforeEach(() => {
    auth.login.mockReset()
    auth.requestWhatsAppCode.mockReset()
    auth.loginWithWhatsApp.mockReset()
    auth.requestWhatsAppCode.mockResolvedValue({
      status: 'accepted',
      message:
        'Se o telefone estiver apto para login, enviaremos um código pelo WhatsApp.',
      retryAfterSeconds: 60,
    })
    auth.loginWithWhatsApp.mockResolvedValue(undefined)
  })

  const renderLogin = () => render(
    <MemoryRouter initialEntries={['/login']}>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/" element={<h1>Portal</h1>} />
      </Routes>
    </MemoryRouter>,
  )

  it('switches between password and WhatsApp without removing password login', async () => {
    const user = userEvent.setup()
    renderLogin()

    expect(screen.getByLabelText(/E-mail/)).toBeInTheDocument()
    await user.click(screen.getByRole(
      'button', { name: 'Entrar com WhatsApp' }))
    expect(screen.getByLabelText(/Telefone \/ WhatsApp/)).toBeInTheDocument()

    await user.click(screen.getByRole(
      'button', { name: 'Voltar ao login por senha' }))
    expect(screen.getByLabelText(/E-mail/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Entrar' }))
      .toBeInTheDocument()
  })

  it('requests, confirms and redirects through the existing auth context', async () => {
    const user = userEvent.setup()
    renderLogin()

    await user.click(screen.getByRole(
      'button', { name: 'Entrar com WhatsApp' }))
    await user.type(
      screen.getByLabelText(/Telefone \/ WhatsApp/),
      '(44) 99999-9999',
    )
    await user.click(screen.getByRole(
      'button', { name: 'Enviar código' }))

    await waitFor(() => expect(auth.requestWhatsAppCode)
      .toHaveBeenCalledWith('(44) 99999-9999'))
    expect(await screen.findByLabelText(/Código de seis dígitos/))
      .toBeInTheDocument()
    expect(screen.getByText(/Se o telefone estiver apto/))
      .toBeInTheDocument()
    expect(screen.getByRole('button', {
      name: /Aguarde 60s para reenviar/,
    })).toBeDisabled()

    await user.type(
      screen.getByLabelText(/Código de seis dígitos/),
      '123456',
    )
    await user.click(screen.getByRole(
      'button', { name: 'Confirmar e entrar' }))

    await waitFor(() => expect(auth.loginWithWhatsApp)
      .toHaveBeenCalledWith('(44) 99999-9999', '123456'))
    expect(await screen.findByRole('heading', { name: 'Portal' }))
      .toBeInTheDocument()
  })

  it('shows the functional unavailable state without revealing account data', async () => {
    auth.requestWhatsAppCode.mockRejectedValue({
      isAxiosError: true,
      response: {
        status: 503,
        data: { code: 'whatsapp_unavailable' },
      },
    })
    const user = userEvent.setup()
    renderLogin()

    await user.click(screen.getByRole(
      'button', { name: 'Entrar com WhatsApp' }))
    await user.type(
      screen.getByLabelText(/Telefone \/ WhatsApp/),
      '(44) 99999-9999',
    )
    await user.click(screen.getByRole(
      'button', { name: 'Enviar código' }))

    expect(await screen.findByText(
      'O login pelo WhatsApp está temporariamente indisponível.',
    )).toBeInTheDocument()
    expect(screen.queryByText(/cadastrado|não verificado|inativo/i))
      .not.toBeInTheDocument()
  })
})
