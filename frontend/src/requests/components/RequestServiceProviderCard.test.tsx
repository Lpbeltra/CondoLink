import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { RequestServiceProviderCard } from './RequestServiceProviderCard'
import type { ServiceProvider } from '../types'

const list = vi.fn()
vi.mock('../api', () => ({ listRequestServiceProviders: (...args: unknown[]) => list(...args), setRequestServiceProvider: vi.fn() }))

const provider: ServiceProvider = { id: 'provider-1', name: 'César', specialty: 'Hidráulica', specialties: ['Hidráulica', 'Manutenções gerais'], companyName: null, phone: '44999999999', isActive: true }
const renderCard = (status: string, current: ServiceProvider | null = null) => render(<RequestServiceProviderCard requestId="request-1" status={status} current={current} history={[]} onUpdated={vi.fn()} />)

describe('RequestServiceProviderCard visibility', () => {
  it('hides outside WaitingForThirdParty without an existing provider', async () => {
    list.mockResolvedValue([provider])
    renderCard('InProgress')
    await waitFor(() => expect(list).toHaveBeenCalled())
    expect(screen.queryByText('Prestador')).not.toBeInTheDocument()
  })

  it('hides in WaitingForThirdParty when there are no eligible providers', async () => {
    list.mockResolvedValue([])
    renderCard('WaitingForThirdParty')
    await waitFor(() => expect(list).toHaveBeenCalled())
    expect(screen.queryByText('Prestador')).not.toBeInTheDocument()
  })

  it('shows in WaitingForThirdParty with candidates and keeps specialty tags in options', async () => {
    list.mockResolvedValue([provider])
    renderCard('WaitingForThirdParty')
    expect(await screen.findByRole('heading', { name: 'Prestador' })).toBeVisible()
    await userEvent.setup().click(screen.getByRole('combobox', { name: 'Prestador' }))
    expect(await screen.findByRole('option', { name: /César.*Hidráulica, Manutenções gerais/ })).toBeVisible()
  })

  it('keeps a linked provider manageable after a status change', async () => {
    list.mockResolvedValue([])
    renderCard('InProgress', provider)
    expect(await screen.findByRole('heading', { name: 'Prestador' })).toBeVisible()
    expect(screen.getAllByText(/César.*Manutenções gerais/).length).toBeGreaterThan(0)
  })
})
