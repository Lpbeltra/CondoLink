import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'

const mocks = vi.hoisted(() => ({
  listManagementRequests: vi.fn(),
  listCompanyRequests: vi.fn(),
  getRequestReport: vi.fn(),
  userRoles: ['PlatformAdmin'] as string[],
  currentCondominium: { roles: ['Manager'] } as { roles: string[] } | null,
  managementRoles: [] as string[],
}))

vi.mock('../requests/api', async (original) => ({
  ...await original<typeof import('../requests/api')>(),
  listManagementRequests: mocks.listManagementRequests,
}))
vi.mock('../managementCompanyRequests/api', () => ({ listRequests: mocks.listCompanyRequests }))
vi.mock('../managementCompanyRequests/realtime', () => ({ useManagementCompanyRequestRealtime: () => undefined }))
vi.mock('../reports/api', () => ({ getRequestReport: mocks.getRequestReport }))
vi.mock('../hooks/useVisiblePolling', () => ({ useVisiblePolling: () => undefined }))
vi.mock('../management/ManagementContext', () => ({
  useManagementContext: () => ({
    activeCondominiumId: 'condo-1',
    activeCondominium: { id: 'condo-1', name: 'Edifício Aurora' },
    usesConsolidatedManagementScope: false,
    condominiums: [{ id: 'condo-1', name: 'Edifício Aurora' }],
    condominiumCount: 1,
    managementRoles: mocks.managementRoles,
    subManagerPermissions: undefined,
    isLoading: false,
    selectCondominium: vi.fn(),
    refresh: vi.fn(),
  }),
}))
vi.mock('../condominiums/CondominiumContext', () => ({
  useCondominium: () => ({ currentCondominium: mocks.currentCondominium }),
}))
vi.mock('../auth/AuthContext', () => ({
  useAuth: () => ({ user: { fullName: 'Maria Silva', roles: mocks.userRoles } }),
}))

import { ManagementCompanyRequestsPage } from './ManagementCompanyRequestsPage'
import { ManagementReportsPage } from './ManagementReportsPage'
import { ManagementRequestsPage } from './ManagementRequestsPage'
import { MorePage } from './MorePage'

const requestData = {
  total: 1,
  page: 1,
  pageSize: 20,
  counts: { open: 1, inProgress: 0, waitingForResident: 0, waitingForManager: 0, waitingForThirdParty: 0, waitingForResidentClosure: 0, resolved: 0, cancelled: 0 },
  items: [{
    id: 'request-1', condominiumId: 'condo-1', condominiumName: 'Edifício Aurora',
    category: { id: 'category-1', name: 'Manutenção' }, targetUnit: null,
    title: 'Portão com defeito', status: 'Open', priority: 'High',
    createdAt: '2026-09-01T10:00:00Z', updatedAt: '2026-09-01T10:00:00Z', resolvedAt: null,
    author: { id: 'resident-1', fullName: 'João Souza' },
  }],
}

const companyData = {
  items: [{
    id: 'company-1', friendlyIdentifier: 'ADM-1', condominiumId: 'condo-1',
    condominiumName: 'Edifício Aurora', managementCompanyName: 'Admin',
    type: 'GeneralQuestion', status: 'Submitted', subject: 'Segunda via',
    createdAt: '2026-09-01T10:00:00Z', updatedAt: '2026-09-01T10:00:00Z',
  }],
  page: 1, pageSize: 20, total: 1, hasMore: false,
}

function setMobile(mobile: boolean) {
  window.matchMedia = vi.fn().mockImplementation((query: string) => ({
    matches: mobile && query.includes('max-width'), media: query, onchange: null,
    addEventListener: vi.fn(), removeEventListener: vi.fn(), addListener: vi.fn(), removeListener: vi.fn(), dispatchEvent: vi.fn(),
  }))
}

const renderPage = (page: React.ReactNode) => render(<MemoryRouter>{page}</MemoryRouter>)

describe('mobile management hierarchy', () => {
  beforeEach(() => {
    setMobile(true)
    mocks.userRoles = ['PlatformAdmin']
    mocks.currentCondominium = { roles: ['Manager'] }
    mocks.managementRoles = []
    mocks.listManagementRequests.mockReset().mockResolvedValue(requestData)
    mocks.listCompanyRequests.mockReset().mockResolvedValue(companyData)
    mocks.getRequestReport.mockReset().mockResolvedValue({
      period: { from: '2026-08-01', to: '2026-09-01', days: 30 },
      summary: { total: 8, open: 3, awaitingFirstResponse: 2, averageFirstResponseHours: 1, averageResolutionHours: 12, resolutionRatePercent: 60 },
      byCategory: [{ categoryId: 'category-1', name: 'Manutenção', total: 8, open: 3, averageResolutionHours: 12 }],
      byPriority: [{ priority: 'High', total: 3, open: 2 }],
      createdPerDay: [{ day: '2026-09-01', created: 1 }],
    })
  })

  it('keeps the request list before summary and opens mobile filters', async () => {
    const user = userEvent.setup()
    renderPage(<ManagementRequestsPage />)
    await screen.findByText('Portão com defeito')
    const list = screen.getByTestId('management-request-list')
    const summary = screen.getByTestId('management-request-summary')
    expect(list.compareDocumentPosition(summary) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
    await user.click(screen.getByRole('button', { name: 'Filtrar' }))
    expect(screen.getByRole('dialog', { name: 'Filtrar atendimentos' })).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Concluir' }))
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
  })

  it('shows an informative empty request state', async () => {
    mocks.listManagementRequests.mockResolvedValue({ ...requestData, total: 0, items: [], counts: { ...requestData.counts, open: 0 } })
    renderPage(<ManagementRequestsPage />)
    expect(await screen.findByText('Nenhuma solicitação ativa encontrada.')).toBeInTheDocument()
  })

  it('places administrator requests before mobile filters and preserves creation', async () => {
    const user = userEvent.setup()
    renderPage(<ManagementCompanyRequestsPage />)
    await screen.findByText('Segunda via')
    const list = screen.getByTestId('management-company-request-list')
    const filter = screen.getByRole('button', { name: 'Filtrar' })
    expect(list.compareDocumentPosition(filter) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Nova solicitação' })).toBeInTheDocument()
    await user.click(filter)
    expect(screen.getByRole('dialog', { name: 'Filtrar solicitações' })).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Concluir' }))
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
  })

  it('shows secondary authorized modules and keeps primary destinations out of Mais', () => {
    renderPage(<MorePage />)
    expect(screen.getByRole('button', { name: 'Administradora' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Overwatch' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Dashboard' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Atendimento' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Assistente' })).not.toBeInTheDocument()
  })

  it('hides Overwatch from Mais without PlatformAdmin permission', () => {
    mocks.userRoles = []
    renderPage(<MorePage />)
    expect(screen.queryByRole('button', { name: 'Overwatch' })).not.toBeInTheDocument()
  })

  it('keeps Overwatch in Mais for a platform administrator without a condominium context', () => {
    mocks.currentCondominium = null
    renderPage(<MorePage />)
    expect(screen.getByRole('button', { name: 'Overwatch' })).toBeInTheDocument()
  })

  it('puts actionable dashboard indicators before historical metrics on mobile', async () => {
    renderPage(<ManagementReportsPage />)
    const attention = await screen.findByRole('heading', { name: 'Atenção agora' })
    const history = screen.getByRole('heading', { name: 'Resumo do período' })
    expect(attention.compareDocumentPosition(history) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
  })

  it('keeps desktop request summary and filters before content', async () => {
    setMobile(false)
    renderPage(<ManagementRequestsPage />)
    await screen.findByText('Portão com defeito')
    expect(screen.getByRole('combobox', { name: 'Status' })).toBeInTheDocument()
    expect(screen.getByText('Abertas').compareDocumentPosition(screen.getByTestId('management-request-list')) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
  })
})
