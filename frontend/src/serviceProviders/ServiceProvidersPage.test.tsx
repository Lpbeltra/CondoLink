import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ServiceProvidersPage } from './ServiceProvidersPage'

const list = vi.fn()
vi.mock('../management/ManagementContext', () => ({ useManagementContext: () => ({ condominiums: [], activeCondominiumId: null }) }))
vi.mock('./api', () => ({ listServiceProviders: (...args: unknown[]) => list(...args), createServiceProvider: vi.fn(), updateServiceProvider: vi.fn() }))

describe('ServiceProvidersPage WhatsApp action', () => {
  beforeEach(() => { list.mockReset(); vi.stubGlobal('open', vi.fn()) })

  it('opens WhatsApp for a provider with a valid phone', async () => {
    list.mockResolvedValue([{ id: 'provider-1', name: 'Cesar', specialty: 'Plumbing', companyName: null, contactName: null, phone: '(44) 99999-9999', isActive: true, isMine: true, condominiums: [] }])
    render(<ServiceProvidersPage />)
    await userEvent.setup().click(await screen.findByRole('button', { name: 'Abrir WhatsApp' }))
    expect(window.open).toHaveBeenCalledWith('https://wa.me/5544999999999?text=', '_blank', 'noopener,noreferrer')
  })

  it('hides WhatsApp when phone is missing or invalid', async () => {
    list.mockResolvedValue([{ id: 'provider-1', name: 'Cesar', specialty: 'Plumbing', companyName: null, contactName: null, phone: 'sem telefone', isActive: true, isMine: true, condominiums: [] }])
    render(<ServiceProvidersPage />)
    await waitFor(() => expect(screen.getByText('Cesar')).toBeVisible())
    expect(screen.queryByRole('button', { name: 'Abrir WhatsApp' })).not.toBeInTheDocument()
  })
})
