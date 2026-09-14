import { render, screen, waitForElementToBeRemoved, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi, beforeEach } from 'vitest'
import { PayslipDistribution } from './PayslipDistribution'

const api = vi.hoisted(() => ({
  listBatches: vi.fn(),
  getBatch: vi.fn(),
  listEmployees: vi.fn(),
  getDistributionSummary: vi.fn(),
  listDeliveries: vi.fn(),
  deleteDocument: vi.fn(),
  deleteBatch: vi.fn(),
}))

vi.mock('./api', async () => {
  const actual = await vi.importActual<typeof import('./api')>('./api')
  return {
    ...actual, listBatches: api.listBatches, getBatch: api.getBatch,
    getDistributionSummary: api.getDistributionSummary, listDeliveries: api.listDeliveries, deleteDocument: api.deleteDocument, deleteBatch: api.deleteBatch,
  }
})
vi.mock('../employees/api', () => ({ listEmployees: api.listEmployees }))

describe('PayslipDistribution administrator batch review', () => {
  it('opens a multi-condominium batch by batchId and loads its detail', async () => {
    const batch = { id: 'batch-1', status: 'ReadyForReview', competenceMonth: 8, competenceYear: 2026, documentCount: 2, createdByName: 'Operator', failureReason: null, processingStage: 'ReadyForReview', processedItems: 2, totalItems: 2, progressPercentage: 100, identifiedCount: 2, needsReviewCount: 0, unidentifiedCount: 0, ignoredCount: 0, confirmedCount: 0 }
    api.listBatches.mockResolvedValue([batch])
    api.getBatch.mockResolvedValue({ batch: { ...batch, selectedCount: 2, condominiumCount: 2 }, documents: [
      { id: 'doc-a', batchId: 'batch-1', employeeId: 'employee-a', employeeName: 'Ana', condominiumId: 'condo-a', condominiumName: 'Monticello', cpf: null, identificationStatus: 'Identified', identificationConfidence: 'High', identificationMethod: 'Cpf' },
      { id: 'doc-b', batchId: 'batch-1', employeeId: 'employee-b', employeeName: 'Bruno', condominiumId: 'condo-b', condominiumName: 'Montpellier', cpf: null, identificationStatus: 'Identified', identificationConfidence: 'High', identificationMethod: 'Cpf' },
    ] })
    api.listEmployees.mockResolvedValue([])

    render(<PayslipDistribution />)
    await screen.findByText('Abrir')
    expect(screen.getByText(/Holerites/)).toBeInTheDocument()
    screen.getByText('Abrir').click()
    await screen.findByText('Monticello')
    expect(api.getBatch).toHaveBeenCalledWith('batch-1')
    expect(api.getBatch.mock.calls[0]).not.toContain('condo-a')
    expect(screen.getByText('Montpellier')).toBeInTheDocument()
  })
})

describe('PayslipDistribution delete a sent payslip', () => {
  const batch = { id: 'batch-2', status: 'Completed', competenceMonth: 8, competenceYear: 2026, documentCount: 1, createdByName: 'Operator', failureReason: null, processingStage: null, processedItems: 1, totalItems: 1, progressPercentage: 100, identifiedCount: 1, needsReviewCount: 0, unidentifiedCount: 0, ignoredCount: 0, confirmedCount: 1 }
  const delivery = { employeeDocumentId: 'doc-read', employeeId: 'employee-a', employeeName: 'Ana', status: 'Read', attemptCount: 1, queuedAt: '2026-08-01T00:00:00Z', sentAt: '2026-08-01T00:00:00Z', deliveredAt: '2026-08-01T00:00:01Z', readAt: '2026-08-01T00:00:02Z', failedAt: null, lastErrorCode: null, lastErrorDescription: null }

  beforeEach(() => {
    vi.clearAllMocks()
    api.listBatches.mockResolvedValue([batch])
    api.getDistributionSummary.mockResolvedValue({ batchId: batch.id, totalConfirmed: 1, ready: 0, noPhone: 0, invalidPhone: 0, alreadyQueuedOrSent: 1 })
    api.listDeliveries.mockResolvedValue([delivery])
  })

  const openDistributionView = async () => {
    render(<PayslipDistribution />)
    await userEvent.click(await screen.findByText('Abrir'))
    await screen.findByText('Ana')
  }

  it('cancel keeps the record — no request is sent', async () => {
    await openDistributionView()
    await userEvent.click(screen.getByRole('button', { name: /Mais ações — Ana/ }))
    await userEvent.click(await screen.findByText('Excluir registro'))
    const dialog = (await screen.findByText('Excluir este holerite?')).closest('.MuiDialog-root') as HTMLElement
    within(dialog).getByText(/não poderá ser reenviado/)
    await userEvent.click(within(dialog).getByText('Cancelar'))

    expect(api.deleteDocument).not.toHaveBeenCalled()
    expect(screen.getByText('Ana')).toBeInTheDocument()
  })

  it('confirming calls the endpoint and marks the row as deleted', async () => {
    api.deleteDocument.mockResolvedValue(undefined)
    await openDistributionView()
    await userEvent.click(screen.getByRole('button', { name: /Mais ações — Ana/ }))
    await userEvent.click(await screen.findByText('Excluir registro'))
    const dialog = (await screen.findByText('Excluir este holerite?')).closest('.MuiDialog-root') as HTMLElement
    await userEvent.click(within(dialog).getByText('Excluir'))

    expect(api.deleteDocument).toHaveBeenCalledWith('batch-2', 'doc-read')
    await screen.findByText('Excluído')
    expect(screen.queryByRole('button', { name: /Mais ações — Ana/ })).not.toBeInTheDocument()
    // Delivery history stays visible — only the management action disappears.
    expect(screen.getByText('Ana')).toBeInTheDocument()
    expect(screen.getByText('Read')).toBeInTheDocument()
  })

  it('shows a friendly error and keeps the row when the delete request fails', async () => {
    api.deleteDocument.mockRejectedValue({ isAxiosError: true, response: { data: { message: 'Envio em andamento.' } } })
    await openDistributionView()
    await userEvent.click(screen.getByRole('button', { name: /Mais ações — Ana/ }))
    await userEvent.click(await screen.findByText('Excluir registro'))
    const dialog = (await screen.findByText('Excluir este holerite?')).closest('.MuiDialog-root') as HTMLElement
    await userEvent.click(within(dialog).getByText('Excluir'))
    await waitForElementToBeRemoved(() => screen.queryByText('Excluir este holerite?'))

    await screen.findByText('Envio em andamento.')
    expect(screen.queryByText('Excluído')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Mais ações — Ana/ })).toBeInTheDocument()
  })
})

describe('PayslipDistribution delete batch', () => {
  it('opens menu, confirms batch deletion and shows empty state', async () => {
    const batch = { id: 'batch-delete', status: 'ReadyForReview', competenceMonth: 9, competenceYear: 2026, documentCount: 4, createdByName: 'Operator', failureReason: null, processingStage: null, processedItems: 4, totalItems: 4, progressPercentage: 100, identifiedCount: 4, needsReviewCount: 0, unidentifiedCount: 0, ignoredCount: 0, confirmedCount: 0 }
    api.listBatches.mockResolvedValue([batch])
    api.deleteBatch.mockResolvedValue(undefined)
    render(<PayslipDistribution />)
    await userEvent.click(await screen.findByRole('button', { name: 'Mais ações' }))
    await userEvent.click(await screen.findByText('Excluir lote'))
    const dialog = await screen.findByText('Excluir este lote de holerites?')
    expect(within(dialog.closest('.MuiDialog-root') as HTMLElement).getByText(/4 documentos serão removidos/)).toBeInTheDocument()
    await userEvent.click(within(dialog.closest('.MuiDialog-root') as HTMLElement).getByText('Excluir lote'))
    expect(api.deleteBatch).toHaveBeenCalledWith('batch-delete')
    await screen.findByText(/Nenhum lote de holerites\./)
  })
})
