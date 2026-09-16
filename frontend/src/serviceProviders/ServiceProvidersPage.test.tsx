import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ServiceProvidersPage } from './ServiceProvidersPage'

const list = vi.fn()
const create = vi.fn()
const context = { condominiums: [], activeCondominiumId: null as string | null }
vi.mock('../management/ManagementContext', () => ({ useManagementContext: () => context }))
vi.mock('./api', () => ({ listServiceProviders: (...args: unknown[]) => list(...args), createServiceProvider: (...args: unknown[]) => create(...args), updateServiceProvider: vi.fn() }))

describe('ServiceProvidersPage WhatsApp action', () => {
  beforeEach(() => { list.mockReset(); create.mockReset(); context.activeCondominiumId = null; vi.stubGlobal('open', vi.fn()) })

  it('explains availability, supports specialty tags, and does not show Contact', async () => {
    list.mockResolvedValue([])
    render(<ServiceProvidersPage />)
    await userEvent.setup().click(await screen.findByRole('button', { name: 'Novo prestador' }))
    const vínculoLabels=screen.getAllByText('Vínculo')
    expect(vínculoLabels[vínculoLabels.length-1]).toBeVisible()
    expect(screen.getByLabelText(/Meu prestador/)).toBeInTheDocument()
    expect(screen.getByLabelText(/Prestador do condom/)).toBeInTheDocument()
    expect(screen.queryByLabelText('Contato')).not.toBeInTheDocument()
    await userEvent.setup().type(screen.getByRole('combobox', { name: 'Especialidades' }), 'Hidráulica{enter}')
    expect(screen.getByText('Hidráulica')).toBeVisible()
  })

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

  it('sends the selected condominium to the server', async () => {
    context.activeCondominiumId = 'condo-1'
    list.mockResolvedValue([])
    render(<ServiceProvidersPage />)
    await waitFor(() => expect(list).toHaveBeenCalledWith(expect.objectContaining({ condominiumId: 'condo-1' })))
  })

  it('keeps loading and does not show an empty state before the response', async () => {
    list.mockReturnValue(new Promise(() => undefined))
    render(<ServiceProvidersPage />)
    expect(await screen.findByText('Carregando prestadores…')).toBeVisible()
    expect(screen.queryByText('Nenhum prestador cadastrado neste contexto.')).not.toBeInTheDocument()
  })

  it('does not turn an API error into an empty state', async () => {
    list.mockRejectedValue(new Error('offline'))
    render(<ServiceProvidersPage />)
    expect(await screen.findByText('Não foi possível carregar prestadores.')).toBeVisible()
    expect(screen.queryByText('Nenhum prestador cadastrado neste contexto.')).not.toBeInTheDocument()
  })

  it('discards a response from the previous condominium', async () => {
    let resolveFirst: (value: unknown[]) => void = () => undefined
    const first = new Promise<unknown[]>(resolve => { resolveFirst = resolve })
    context.activeCondominiumId = 'condo-a'
    list.mockReturnValueOnce(first).mockResolvedValueOnce([{ id: 'b', name: 'Prestador B', specialty: 'Elétrica', specialties: [], companyName: null, phone: 'sem telefone', isActive: true, isMine: false, condominiums: [] }])
    const view = render(<ServiceProvidersPage />)
    await waitFor(() => expect(list).toHaveBeenCalledWith(expect.objectContaining({ condominiumId: 'condo-a' })))
    context.activeCondominiumId = 'condo-b'
    view.rerender(<ServiceProvidersPage />)
    expect(await screen.findByText('Prestador B')).toBeVisible()
    resolveFirst([{ id: 'a', name: 'Prestador A', specialty: 'Elétrica', specialties: [], companyName: null, phone: 'sem telefone', isActive: true, isMine: false, condominiums: [] }])
    await new Promise(resolve => window.setTimeout(resolve, 0))
    expect(screen.queryByText('Prestador A')).not.toBeInTheDocument()
  })

  it('opens the contextual detail and secondary actions', async () => {
    list.mockResolvedValue([{ id: 'provider-1', name: 'Cesar', specialty: 'Elétrica', specialties: ['Elétrica', 'Manutenção'], companyName: 'Cesar Ltda', phone: '(44) 99999-9999', email: 'cesar@test.local', pixKey: 'cesar@test.local', pixKeyType: 'Email', notes: 'Atende emergências', isActive: true, isMine: true, condominiums: [] }])
    render(<ServiceProvidersPage />)
    const user= userEvent.setup()
    await user.click(await screen.findByRole('listitem', { name: 'Ver detalhes de Cesar' }))
    expect(screen.getAllByText('cesar@test.local').length).toBeGreaterThanOrEqual(1)
    expect(screen.getByText('Atende emergências')).toBeVisible()
    await user.click(screen.getByRole('button', { name: 'Fechar detalhes' }))
    await waitFor(() => expect(screen.getByRole('button', { name: 'Mais ações de Cesar' })).toBeVisible())
    await user.click(screen.getByRole('button', { name: 'Mais ações de Cesar' }))
    expect(screen.getByRole('menuitem', { name: /Editar/ })).toBeVisible()
  })
})
