import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'

const mocks = vi.hoisted(() => ({
  getRequestReport: vi.fn(),
  listManagementRequests: vi.fn(),
  context: {
    activeCondominiumId: 'condo-1' as string | null,
    activeCondominium: { id: 'condo-1', name: 'Aurora' } as { id: string; name: string } | null,
    usesConsolidatedManagementScope: false,
  },
}))

vi.mock('../reports/api', () => ({ getRequestReport: mocks.getRequestReport }))
vi.mock('../requests/api', () => ({ listManagementRequests: mocks.listManagementRequests }))
vi.mock('../management/ManagementContext', () => ({ useManagementContext: () => mocks.context }))
vi.mock('../auth/AuthContext', () => ({ useAuth: () => ({ user: { fullName: 'Maria Silva' } }) }))

import { ManagementReportsPage } from './ManagementReportsPage'

const report = (total = 4) => ({
  period: { from: '2026-08-01', to: '2026-08-30', days: 30 },
  summary: { total, open: 1, awaitingFirstResponse: 1, averageFirstResponseHours: 2, averageResolutionHours: 12, resolutionRatePercent: 50 },
  byCategory: [], byPriority: [], createdPerDay: [{ day: '2026-08-01', created: total }],
})

const current = {
  total: 5, page: 1, pageSize: 1,
  counts: { open: 1, inProgress: 2, waitingForResident: 1, waitingForManager: 3, waitingForThirdParty: 1, waitingForResidentClosure: 2, resolved: 4, cancelled: 1 },
  items: [],
}

function renderPage() {
  return render(<MemoryRouter><ManagementReportsPage /></MemoryRouter>)
}

describe('ManagementReportsPage foundation', () => {
  beforeEach(() => {
    mocks.context.activeCondominiumId = 'condo-1'
    mocks.context.activeCondominium = { id: 'condo-1', name: 'Aurora' }
    mocks.context.usesConsolidatedManagementScope = false
    mocks.getRequestReport.mockReset().mockResolvedValue(report())
    mocks.listManagementRequests.mockReset().mockImplementation(async (filters: { priority?: string }) => filters.priority === 'Urgent' ? { ...current, total: 2 } : current)
  })

  it('scopes history and current signals to selected condominium', async () => {
    renderPage()

    expect(await screen.findByText('Em aberto agora')).toBeInTheDocument()
    expect(mocks.getRequestReport).toHaveBeenCalledWith(30, 'condo-1')
    expect(mocks.listManagementRequests).toHaveBeenCalledWith({ condominiumId: 'condo-1', pageSize: 1 })
    expect(mocks.listManagementRequests).toHaveBeenCalledWith({ condominiumId: 'condo-1', pageSize: 1, priority: 'Urgent' })
  })

  it('keeps operational signals independent from selected historical period', async () => {
    const user = userEvent.setup()
    renderPage()
    await screen.findByText('Em aberto agora')
    const operationalCalls = mocks.listManagementRequests.mock.calls.length

    await user.click(screen.getByRole('button', { name: '7 dias' }))
    await waitFor(() => expect(mocks.getRequestReport).toHaveBeenCalledWith(7, 'condo-1'))
    expect(mocks.listManagementRequests).toHaveBeenCalledTimes(operationalCalls)
  })

  it('uses consolidated scope without condominiumId', async () => {
    mocks.context.activeCondominiumId = null
    mocks.context.activeCondominium = null
    mocks.context.usesConsolidatedManagementScope = true
    renderPage()

    expect(await screen.findByText('Em aberto agora')).toBeInTheDocument()
    expect(mocks.getRequestReport).toHaveBeenCalledWith(30, undefined)
    expect(mocks.listManagementRequests).toHaveBeenCalledWith({ condominiumId: undefined, pageSize: 1 })
  })

  it('does not show a stale historical response after condominium change', async () => {
    let resolveFirst!: (value: ReturnType<typeof report>) => void
    let resolveSecond!: (value: ReturnType<typeof report>) => void
    const first = new Promise<ReturnType<typeof report>>(resolve => { resolveFirst = resolve })
    const second = new Promise<ReturnType<typeof report>>(resolve => { resolveSecond = resolve })
    mocks.getRequestReport.mockImplementation((_days: number, condominiumId?: string) => condominiumId === 'condo-1' ? first : second)
    const view = renderPage()

    mocks.context.activeCondominiumId = 'condo-2'
    mocks.context.activeCondominium = { id: 'condo-2', name: 'Bosque' }
    view.rerender(<MemoryRouter><ManagementReportsPage /></MemoryRouter>)
    resolveSecond(report(20))
    expect(await screen.findByText('20')).toBeInTheDocument()

    resolveFirst(report(4))
    await waitFor(() => expect(screen.getByText('20')).toBeInTheDocument())
    expect(screen.queryByText('4')).not.toBeInTheDocument()
  })

  it('keeps attention visible when selected period has no new requests', async () => {
    mocks.getRequestReport.mockResolvedValue(report(0))
    renderPage()

    expect(await screen.findByText('Nenhum novo atendimento neste período')).toBeInTheDocument()
    expect(screen.getByText('Em aberto agora')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Conclusão pendente/ })).toHaveTextContent('2')
    expect(screen.queryByText('Solicitações por dia')).not.toBeInTheDocument()
  })

  it('exposes operational drill-down filters', async () => {
    renderPage()
    await screen.findByText('Em aberto agora')

    expect(screen.getByRole('link', { name: /Dar andamento/ })).toHaveAttribute('href', '/management/requests?status=WaitingForManager')
    expect(screen.getByRole('link', { name: /Conclusão pendente/ })).toHaveAttribute('href', '/management/requests?status=WaitingForResidentClosure')
    expect(screen.getByRole('link', { name: /Urgentes/ })).toHaveAttribute('href', '/management/requests?priority=Urgent')
  })
})
