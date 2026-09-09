import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, beforeEach, vi } from 'vitest'
import { RequestServiceProviderCard } from './RequestServiceProviderCard'
import type { ServiceProvider } from '../types'

const list = vi.fn()
const setProvider = vi.fn()
vi.mock('../api', () => ({ listRequestServiceProviders: (...args: unknown[]) => list(...args), setRequestServiceProvider: (...args: unknown[]) => setProvider(...args) }))

const provider: ServiceProvider = { id: 'provider-1', name: 'César', specialty: 'Hidráulica', specialties: ['Hidráulica', 'Manutenções gerais'], companyName: null, phone: '44999999999', isActive: true }
const renderCard = (status: string, current: ServiceProvider | null = null, onUpdated = vi.fn()) => render(<RequestServiceProviderCard requestId="request-1" status={status} current={current} history={[]} onUpdated={onUpdated} />)

describe('RequestServiceProviderCard visibility and removal', () => {
  beforeEach(() => { list.mockReset(); setProvider.mockReset(); setProvider.mockResolvedValue({}) })

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
    expect(screen.getByRole('button', { name: 'Desvincular prestador' })).toBeVisible()
  })

  it('confirms removal, keeps status implicit, and sends a null link only after confirmation', async () => {
    list.mockResolvedValue([provider])
    const onUpdated = vi.fn()
    renderCard('WaitingForThirdParty', provider, onUpdated)
    const user = userEvent.setup()
    await user.click(await screen.findByRole('button', { name: 'Desvincular prestador' }))
    expect(screen.getByText(/histórico deste atendimento será preservado/i)).toBeVisible()
    await user.click(screen.getByRole('button', { name: 'Cancelar' }))
    expect(setProvider).not.toHaveBeenCalled()
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    await user.click(screen.getByRole('button', { name: 'Desvincular prestador' }))
    await user.click(screen.getByRole('button', { name: /^Desvincular$/ }))
    await waitFor(() => expect(setProvider).toHaveBeenCalledWith('request-1', null))
    expect(onUpdated).toHaveBeenCalled()
  })

  it('keeps linked inactive provider removable', async () => {
    list.mockResolvedValue([])
    renderCard('WaitingForThirdParty', { ...provider, isActive: false })
    expect(await screen.findByText(/Inativo/)).toBeVisible()
    expect(screen.getByRole('button', { name: 'Desvincular prestador' })).toBeEnabled()
  })

  it('keeps current provider when removal fails', async () => {
    list.mockResolvedValue([provider]); setProvider.mockRejectedValueOnce(new Error('failed'))
    renderCard('WaitingForThirdParty', provider)
    const user = userEvent.setup()
    await user.click(await screen.findByRole('button', { name: 'Desvincular prestador' }))
    await user.click(screen.getByRole('button', { name: /^Desvincular$/ }))
    expect(await screen.findByText('Não foi possível desvincular o prestador.')).toBeVisible()
    expect(screen.getAllByText(/César.*Hidráulica/).length).toBeGreaterThan(0)
  })
})
