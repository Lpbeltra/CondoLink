import { fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'

const auth = vi.hoisted(() => ({
  user: null,
  isInitializing: false,
  login: vi.fn(),
  logout: vi.fn(),
}))
const http = vi.hoisted(() => ({ post: vi.fn() }))

vi.mock('../auth/AuthContext', () => ({ useAuth: () => auth }))
vi.mock('../services/api', () => ({ api: http }))
vi.mock('../theme/ThemeModeToggle', () => ({
  ThemeModeToggle: () => null,
}))

import { ChangePasswordPage } from './ChangePasswordPage'
import { LoginPage } from './LoginPage'

describe('first access frontend flow', () => {
  beforeEach(() => {
    auth.login.mockReset()
    http.post.mockReset()
  })

  it('redirects a temporary-password login to the change screen', async () => {
    auth.login.mockResolvedValue({
      requiresPasswordChange: true,
      email: 'pessoa@example.com',
      temporaryPassword: 'Temporaria1',
    })
    render(
      <MemoryRouter initialEntries={['/login']}>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/change-password" element={<ChangePasswordPage />} />
        </Routes>
      </MemoryRouter>,
    )

    fireEvent.change(screen.getByLabelText(/E-mail/), {
      target: { value: 'pessoa@example.com' },
    })
    fireEvent.change(screen.getByLabelText(/^Senha\s/), {
      target: { value: 'Temporaria1' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Entrar' }))

    expect(await screen.findByRole('heading', {
      name: 'Alterar senha',
    })).toBeInTheDocument()
    expect(screen.getByLabelText(/E-mail/)).toHaveValue('pessoa@example.com')
    expect(screen.getByLabelText(/Senha temporária/)).toHaveValue('Temporaria1')
  })

  it('validates confirmation and enters the portal after changing the password', async () => {
    http.post.mockResolvedValue({ data: { message: 'ok' } })
    auth.login.mockResolvedValue({ requiresPasswordChange: false })
    const user = userEvent.setup()
    render(
      <MemoryRouter
        initialEntries={[{
          pathname: '/change-password',
          state: {
            email: 'pessoa@example.com',
            temporaryPassword: 'Temporaria1',
          },
        }]}
      >
        <Routes>
          <Route path="/change-password" element={<ChangePasswordPage />} />
          <Route path="/" element={<h1>Portal</h1>} />
        </Routes>
      </MemoryRouter>,
    )

    await user.type(screen.getByLabelText(/Nova senha/), 'NovaSenha2')
    await user.type(
      screen.getByLabelText(/Confirmar nova senha/),
      'Diferente2',
    )
    expect(screen.getByText(
      'A confirmação da senha não confere.',
    )).toBeInTheDocument()
    expect(screen.getByRole('button', {
      name: 'Atualizar senha',
    })).toBeDisabled()

    await user.clear(screen.getByLabelText(/Confirmar nova senha/))
    await user.type(
      screen.getByLabelText(/Confirmar nova senha/),
      'NovaSenha2',
    )
    await user.click(screen.getByRole('button', {
      name: 'Atualizar senha',
    }))

    expect(http.post).toHaveBeenCalledWith(
      '/auth/change-temporary-password',
      {
        email: 'pessoa@example.com',
        temporaryPassword: 'Temporaria1',
        newPassword: 'NovaSenha2',
        confirmation: 'NovaSenha2',
      },
    )
    expect(auth.login).toHaveBeenCalledWith(
      'pessoa@example.com',
      'NovaSenha2',
    )
    expect(await screen.findByRole('heading', {
      name: 'Portal',
    })).toBeInTheDocument()
  })

  it('toggles each password independently without submitting the form', async () => {
    const user = userEvent.setup()
    render(
      <MemoryRouter initialEntries={['/change-password']}>
        <Routes>
          <Route path="/change-password" element={<ChangePasswordPage />} />
        </Routes>
      </MemoryRouter>,
    )

    const temporary = screen.getByLabelText(/Senha temporária/)
    const newPassword = screen.getByLabelText(/^Nova senha/)
    const confirmation = screen.getByLabelText(/Confirmar nova senha/)
    expect(temporary).toHaveAttribute('type', 'password')
    expect(newPassword).toHaveAttribute('type', 'password')
    expect(confirmation).toHaveAttribute('type', 'password')

    const toggles = screen.getAllByRole('button', { name: 'Exibir senha' })
    await user.click(toggles[0])
    expect(temporary).toHaveAttribute('type', 'text')
    expect(newPassword).toHaveAttribute('type', 'password')
    expect(http.post).not.toHaveBeenCalled()

    await user.click(toggles[1])
    await user.click(toggles[2])
    expect(newPassword).toHaveAttribute('type', 'text')
    expect(confirmation).toHaveAttribute('type', 'text')
    expect(screen.getAllByRole('button', { name: 'Ocultar senha' })).toHaveLength(3)
    expect(http.post).not.toHaveBeenCalled()
  })
})
