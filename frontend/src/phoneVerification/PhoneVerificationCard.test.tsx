import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { PhoneVerificationCard } from './PhoneVerificationCard'

const getStatus = vi.fn()
const start = vi.fn()
vi.mock('./api', () => ({
  getPhoneVerificationStatus: () => getStatus(),
  startPhoneVerification: () => start(),
}))

describe('PhoneVerificationCard', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    getStatus.mockResolvedValue({
      maskedPhoneNumber: '***9999',
      confirmed: false,
      activeChallenge: false,
      expiresAt: null,
      canResend: true,
      canResendAt: null,
    })
    start.mockResolvedValue({
      status: 'started',
      expiresAt: new Date(Date.now() + 600_000).toISOString(),
    })
  })

  it('starts confirmation without displaying a code', async () => {
    const user = userEvent.setup()
    render(<PhoneVerificationCard />)

    await user.click(await screen.findByRole(
      'button', { name: 'Confirmar pelo WhatsApp' }))

    await waitFor(() => expect(start).toHaveBeenCalledOnce())
    expect(screen.getByText(/Código enviado/)).toBeInTheDocument()
    expect(screen.queryByText(/\b\d{6}\b/)).not.toBeInTheDocument()
  })

  it('shows a confirmed state without an action button', async () => {
    getStatus.mockResolvedValue({
      maskedPhoneNumber: '***9999',
      confirmed: true,
      activeChallenge: false,
      expiresAt: null,
      canResend: true,
      canResendAt: null,
    })
    render(<PhoneVerificationCard />)

    expect(await screen.findByText('Confirmado')).toBeInTheDocument()
    expect(screen.queryByRole(
      'button', { name: 'Confirmar pelo WhatsApp' })).not.toBeInTheDocument()
  })
})
