import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'

const http = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
}))
const logout = vi.hoisted(() => vi.fn())

vi.mock('../services/api', () => ({ api: http }))
vi.mock('../auth/AuthContext', () => ({
  useAuth: () => ({
    user: {
      id: 'user-id',
      fullName: 'Pessoa Teste',
      email: 'pessoa@example.com',
      roles: ['Resident'],
    },
    logout,
  }),
}))
vi.mock('../condominiums/CondominiumContext', () => ({
  useCondominium: () => ({ condominiums: [] }),
}))
vi.mock('../management/ManagementContext', () => ({
  useOptionalManagementContext: () => null,
}))
vi.mock('../notifications/NotificationBell', () => ({
  NotificationBell: () => null,
}))
vi.mock('../theme/ThemeModeToggle', () => ({
  ThemeModeToggle: () => null,
}))
vi.mock('../components/Brand', () => ({
  Brand: () => <span>Comvy</span>,
}))
vi.mock('./CondominiumSwitcher', () => ({
  CondominiumSwitcher: () => null,
}))

import { AppHeader } from './AppHeader'

describe('AppHeader phone verification dialog lifecycle', () => {
  beforeEach(() => {
    http.get.mockReset()
    http.post.mockReset()
    http.get.mockResolvedValue({ data: {
      maskedPhoneNumber: '***9999',
      confirmed: false,
      activeChallenge: false,
      expiresAt: null,
      canResend: true,
      canResendAt: null,
    } })
    http.post.mockResolvedValue({ data: {
      status: 'started',
      expiresAt: new Date(Date.now() + 600_000).toISOString(),
    } })
  })

  it('closes the menu but keeps the dialog mounted and can start the POST', async () => {
    const user = userEvent.setup()
    render(<MemoryRouter><AppHeader /></MemoryRouter>)

    await user.click(screen.getByRole('button', {
      name: 'Abrir menu do usuário',
    }))
    await user.click(await screen.findByRole('menuitem', {
      name: 'Confirmar WhatsApp',
    }))

    await waitFor(() => expect(screen.queryByRole('menu')).not.toBeInTheDocument())
    expect(screen.getByRole('dialog', {
      name: 'Confirmar WhatsApp',
    })).toBeInTheDocument()

    const start = await screen.findByRole('button', {
      name: 'Confirmar pelo WhatsApp',
    })
    expect(start).toBeEnabled()
    await user.click(start)

    await waitFor(() => expect(http.post).toHaveBeenCalledWith(
      '/users/me/phone-verification',
    ))
    expect(screen.getByRole('dialog', {
      name: 'Confirmar WhatsApp',
    })).toBeInTheDocument()
  })
})
