import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { AuthProvider } from './AuthProvider'
import { useAuth } from './AuthContext'

const http = vi.hoisted(() => ({
  post: vi.fn(),
  get: vi.fn(),
  defaults: { headers: { common: {} as Record<string, string> } },
}))

vi.mock('../services/api', () => ({ api: http }))

function Consumer() {
  const { user, requestWhatsAppCode, loginWithWhatsApp } = useAuth()
  return (
    <>
      <button onClick={() => void requestWhatsAppCode('(44) 99999-9999')}>
        request
      </button>
      <button onClick={() => void loginWithWhatsApp(
        '(44) 99999-9999',
        '123456',
      )}>
        login
      </button>
      <span>{user?.fullName ?? 'anonymous'}</span>
    </>
  )
}

describe('WhatsApp login in AuthProvider', () => {
  const values = new Map<string, string>()

  beforeEach(() => {
    values.clear()
    Object.defineProperty(globalThis, 'localStorage', {
      configurable: true,
      value: {
        getItem: (key: string) => values.get(key) ?? null,
        setItem: (key: string, value: string) => values.set(key, value),
        removeItem: (key: string) => values.delete(key),
        clear: () => values.clear(),
      },
    })
    http.post.mockReset()
    http.get.mockReset()
    localStorage.clear()
    delete http.defaults.headers.common.Authorization
  })

  it('stores and hydrates WhatsApp authentication like password login', async () => {
    http.post.mockResolvedValue({
      data: {
        requiresPasswordChange: false,
        accessToken: 'header.payload.signature',
        tokenType: 'Bearer',
        expiresIn: 3600,
        user: {
          id: 'user-1',
          fullName: 'Pessoa',
          email: 'pessoa@example.com',
          isActive: true,
          roles: ['Resident'],
        },
      },
    })
    http.get.mockResolvedValue({
      data: {
        id: 'user-1',
        fullName: 'Pessoa',
        email: 'pessoa@example.com',
        isActive: true,
        roles: ['Resident'],
      },
    })
    const user = userEvent.setup()
    render(<AuthProvider><Consumer /></AuthProvider>)

    await user.click(await screen.findByRole('button', { name: 'login' }))

    await waitFor(() => expect(screen.getByText('Pessoa'))
      .toBeInTheDocument())
    expect(http.post).toHaveBeenCalledWith(
      '/auth/whatsapp/confirm',
      {
        phoneNumber: '(44) 99999-9999',
        code: '123456',
      },
    )
    expect(localStorage.getItem('condolink.accessToken'))
      .toBe('header.payload.signature')
    expect(http.defaults.headers.common.Authorization)
      .toBe('Bearer header.payload.signature')
  })

  it('keeps the unauthenticated request-code route exclusive to login', async () => {
    http.post.mockResolvedValue({
      data: {
        status: 'accepted',
        message: 'Código solicitado.',
        retryAfterSeconds: 60,
      },
    })
    const user = userEvent.setup()
    render(<AuthProvider><Consumer /></AuthProvider>)

    await user.click(await screen.findByRole('button', { name: 'request' }))

    expect(http.post).toHaveBeenCalledWith(
      '/auth/whatsapp/request-code',
      { phoneNumber: '(44) 99999-9999' },
    )
  })
})
