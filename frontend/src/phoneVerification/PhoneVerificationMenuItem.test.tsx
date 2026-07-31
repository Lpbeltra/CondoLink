import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'

const phoneApi = vi.hoisted(() => ({
  getStatus: vi.fn(),
}))

vi.mock('./api', () => ({
  getPhoneVerificationStatus: () => phoneApi.getStatus(),
}))
vi.mock('./PhoneVerificationCard', () => ({
  PhoneVerificationCard: () => <div>Fluxo de confirmação existente</div>,
}))

import { PhoneVerificationMenuItem } from './PhoneVerificationMenuItem'

describe('phone verification user menu item', () => {
  beforeEach(() => phoneApi.getStatus.mockReset())

  it.each(['morador', 'síndico', 'Platform Admin'])(
    'shows the role-independent action for %s',
    async () => {
      phoneApi.getStatus.mockResolvedValue({
        maskedPhoneNumber: '+55*******01',
        confirmed: false,
        activeChallenge: false,
        expiresAt: null,
        canResend: true,
        canResendAt: null,
      })
      render(<MemoryRouter><PhoneVerificationMenuItem closeMenu={vi.fn()} /></MemoryRouter>)
      expect(await screen.findByText('Confirmar WhatsApp')).toBeInTheDocument()
    },
  )

  it('opens the existing flow from the responsive user menu', async () => {
    phoneApi.getStatus.mockResolvedValue({
      maskedPhoneNumber: '+55*******01',
      confirmed: false,
      activeChallenge: false,
      expiresAt: null,
      canResend: true,
      canResendAt: null,
    })
    const user = userEvent.setup()
    render(<MemoryRouter><PhoneVerificationMenuItem closeMenu={vi.fn()} /></MemoryRouter>)
    await user.click(await screen.findByText('Confirmar WhatsApp'))
    expect(screen.getByText('Fluxo de confirmação existente')).toBeInTheDocument()
  })

  it('shows confirmed status without requesting another code', async () => {
    phoneApi.getStatus.mockResolvedValue({
      maskedPhoneNumber: '+55*******01',
      confirmed: true,
      activeChallenge: false,
      expiresAt: null,
      canResend: false,
      canResendAt: null,
    })
    render(<MemoryRouter><PhoneVerificationMenuItem closeMenu={vi.fn()} /></MemoryRouter>)
    expect(await screen.findByText('WhatsApp confirmado')).toBeInTheDocument()
    expect(screen.getByText('WhatsApp confirmado').closest('[role="menuitem"]'))
      .toHaveAttribute('aria-disabled', 'true')
  })

  it('updates the menu immediately after successful confirmation', async () => {
    phoneApi.getStatus.mockResolvedValue({
      maskedPhoneNumber: '+55*******01',
      confirmed: false,
      activeChallenge: true,
      expiresAt: null,
      canResend: false,
      canResendAt: null,
    })
    render(<MemoryRouter><PhoneVerificationMenuItem closeMenu={vi.fn()} /></MemoryRouter>)
    expect(await screen.findByText('Confirmar WhatsApp')).toBeInTheDocument()

    act(() => {
      window.dispatchEvent(new CustomEvent(
        'condolink:phone-verification-updated',
        { detail: {
          maskedPhoneNumber: '+55*******01',
          confirmed: true,
          activeChallenge: false,
          expiresAt: null,
          canResend: false,
          canResendAt: null,
        } },
      ))
    })

    expect(await screen.findByText('WhatsApp confirmado')).toBeInTheDocument()
  })
})
