import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { ManagementRequestItem, ManagementRequestsResponse } from '../requests/types'

const mocks = vi.hoisted(() => ({
  listManagementRequests: vi.fn(),
  selectCondominium: vi.fn(),
  refresh: vi.fn(),
  context: {
    activeCondominiumId: 'condo-1' as string | null,
    activeCondominium: { id: 'condo-1', name: 'Aurora', isActive: true } as { id: string; name: string; isActive: boolean } | null,
    usesConsolidatedManagementScope: false,
    condominiums: [
      { id: 'condo-1', name: 'Aurora', isActive: true },
      { id: 'condo-2', name: 'Bosque', isActive: true },
    ],
    isLoading: false,
  },
}))

vi.mock('../requests/api', async original => ({
  ...await original<typeof import('../requests/api')>(),
  listManagementRequests: mocks.listManagementRequests,
}))
vi.mock('../management/ManagementContext', () => ({
  useManagementContext: () => ({
    ...mocks.context,
    selectCondominium: mocks.selectCondominium,
    refresh: mocks.refresh,
  }),
}))
vi.mock('../hooks/useVisiblePolling', () => ({ useVisiblePolling: () => undefined }))

import { ManagementRequestsPage } from './ManagementRequestsPage'

const items: ManagementRequestItem[] = [
  {
    id: 'open-request', protocol: 'AT-10', condominiumId: 'condo-1', condominiumName: 'Aurora',
    category: { id: 'maintenance', name: 'Manutenção' },
    targetUnit: { id: 'unit-1', identifier: '101', block: 'A' },
    title: 'Portão travado', status: 'Open', priority: 'High',
    createdAt: '2026-09-10T10:00:00Z', updatedAt: '2026-09-10T10:00:00Z', resolvedAt: null,
    author: { id: 'resident-1', fullName: 'Maria Lima' },
  },
  {
    id: 'progress-request', protocol: 'AT-20', condominiumId: 'condo-1', condominiumName: 'Aurora',
    category: { id: 'security', name: 'Segurança' }, targetUnit: null,
    title: 'Câmera sem sinal', status: 'InProgress', priority: 'Normal',
    createdAt: '2026-09-12T10:00:00Z', updatedAt: '2026-09-12T10:00:00Z', resolvedAt: null,
    author: { id: 'resident-2', fullName: 'João Souza' },
  },
  {
    id: 'resolved-request', protocol: 'AT-30', condominiumId: 'condo-1', condominiumName: 'Aurora',
    category: { id: 'maintenance', name: 'Manutenção' }, targetUnit: null,
    title: 'Vazamento resolvido', status: 'Resolved', priority: 'Urgent',
    createdAt: '2026-09-08T10:00:00Z', updatedAt: '2026-09-13T10:00:00Z', resolvedAt: '2026-09-13T10:00:00Z',
    author: { id: 'resident-3', fullName: 'Ana Costa' },
  },
  {
    id: 'bosque-request', protocol: 'AT-40', condominiumId: 'condo-2', condominiumName: 'Bosque',
    category: { id: 'access', name: 'Acesso' }, targetUnit: null,
    title: 'Cadastro de visitante', status: 'WaitingForManager', priority: 'Normal',
    createdAt: '2026-09-09T10:00:00Z', updatedAt: '2026-09-09T10:00:00Z', resolvedAt: null,
    author: { id: 'resident-4', fullName: 'Carlos Melo' },
  },
]

const counts: ManagementRequestsResponse['counts'] = {
  open: 1, inProgress: 1, waitingForResident: 0, waitingForManager: 0,
  waitingForThirdParty: 0, waitingForResidentClosure: 0, resolved: 1, cancelled: 0,
}

function response(filteredItems = items): ManagementRequestsResponse {
  return { total: filteredItems.length, page: 1, pageSize: 20, counts, items: filteredItems }
}

function renderPage(initialEntry = '/management/requests') {
  return render(
    <MemoryRouter initialEntries={[initialEntry]}>
      <Routes>
        <Route path="/management/requests" element={<ManagementRequestsPage />} />
        <Route path="/management/requests/:requestId" element={<div>Detalhe carregado</div>} />
      </Routes>
    </MemoryRouter>,
  )
}

async function choose(label: string, option: string) {
  const user = userEvent.setup()
  await user.click(screen.getByRole('combobox', { name: label }))
  await user.click(await screen.findByRole('option', { name: option }))
}

describe('ManagementRequestsPage capabilities', () => {
  beforeEach(() => {
    mocks.context.activeCondominiumId = 'condo-1'
    mocks.context.activeCondominium = { id: 'condo-1', name: 'Aurora', isActive: true }
    mocks.context.usesConsolidatedManagementScope = false
    mocks.selectCondominium.mockReset().mockResolvedValue(undefined)
    mocks.refresh.mockReset().mockResolvedValue(undefined)
    mocks.listManagementRequests.mockReset().mockImplementation(async filters => response(items.filter(item =>
      (!filters.condominiumId || item.condominiumId === filters.condominiumId)
      && (!filters.status || item.status === filters.status)
      && (!filters.priority || item.priority === filters.priority))))
  })

  it('loads the active condominium, searches useful fields and opens a request', async () => {
    const user = userEvent.setup()
    renderPage()

    expect(await screen.findByText('Portão travado')).toBeVisible()
    expect(mocks.listManagementRequests).toHaveBeenCalledWith(expect.objectContaining({ condominiumId: 'condo-1' }))

    await user.type(screen.getByRole('textbox', { name: 'Buscar' }), 'João')
    expect(screen.getByText('Câmera sem sinal')).toBeVisible()
    expect(screen.queryByText('Portão travado')).not.toBeInTheDocument()

    await user.clear(screen.getByRole('textbox', { name: 'Buscar' }))
    await user.click(screen.getByText('Portão travado'))
    expect(await screen.findByText('Detalhe carregado')).toBeVisible()
  })

  it('applies status and priority filters through executable controls', async () => {
    renderPage()
    await screen.findByText('Portão travado')

    await choose('Status', 'Em andamento')
    await waitFor(() => expect(mocks.listManagementRequests).toHaveBeenLastCalledWith(expect.objectContaining({
      status: 'InProgress', condominiumId: 'condo-1',
    })))
    expect(await screen.findByText('Câmera sem sinal')).toBeVisible()

    await choose('Prioridade', 'Normal')
    await waitFor(() => expect(mocks.listManagementRequests).toHaveBeenLastCalledWith(expect.objectContaining({
      status: 'InProgress', priority: 'Normal', condominiumId: 'condo-1',
    })))
  })

  it('uses a summary indicator as a status filter', async () => {
    const user = userEvent.setup()
    renderPage()
    await screen.findByText('Portão travado')

    await user.click(screen.getByRole('button', { name: 'Filtrar por Resolvidas' }))
    await waitFor(() => expect(mocks.listManagementRequests).toHaveBeenLastCalledWith(expect.objectContaining({ status: 'Resolved' })))
    expect(await screen.findByText('Vazamento resolvido')).toBeVisible()
    expect(screen.queryByText('Portão travado')).not.toBeInTheDocument()
  })

  it('sorts the rendered results using the selected order', async () => {
    renderPage()
    await screen.findByText('Portão travado')

    await choose('Ordenar por', 'Urgência')
    await choose('Ordem', 'Crescente')

    const list = within(screen.getByTestId('management-request-list'))
    const actionNames = list.getAllByRole('button').map(button => button.textContent ?? '')
    expect(actionNames.findIndex(name => name.includes('Câmera sem sinal')))
      .toBeLessThan(actionNames.findIndex(name => name.includes('Portão travado')))
  })

  it('uses consolidated scope for Todos and removes condominium-specific category UI', async () => {
    mocks.context.activeCondominiumId = null
    mocks.context.activeCondominium = null
    mocks.context.usesConsolidatedManagementScope = true
    renderPage()

    expect(await screen.findByText('Portão travado')).toBeVisible()
    expect(mocks.listManagementRequests).toHaveBeenCalledWith(expect.objectContaining({ condominiumId: undefined }))
    expect(screen.queryByRole('combobox', { name: 'Categoria' })).not.toBeInTheDocument()
    expect(screen.getAllByText('Aurora').length).toBeGreaterThan(0)
    expect(screen.getByText('Bosque')).toBeVisible()
  })
})
