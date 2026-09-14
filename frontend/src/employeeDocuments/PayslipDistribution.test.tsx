import { render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { PayslipDistribution } from './PayslipDistribution'

const api = vi.hoisted(() => ({
  listBatches: vi.fn(),
  getBatch: vi.fn(),
  listEmployees: vi.fn(),
}))

vi.mock('./api', async () => {
  const actual = await vi.importActual<typeof import('./api')>('./api')
  return { ...actual, listBatches: api.listBatches, getBatch: api.getBatch }
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

    render(<PayslipDistribution selectedEmployeeIds={['employee-a', 'employee-b']} />)
    await screen.findByText('Abrir')
    expect(screen.getByText(/Holerites/)).toBeInTheDocument()
    screen.getByText('Abrir').click()
    await screen.findByText('Monticello')
    expect(api.getBatch).toHaveBeenCalledWith('batch-1')
    expect(api.getBatch.mock.calls[0]).not.toContain('condo-a')
    expect(screen.getByText('Montpellier')).toBeInTheDocument()
  })
})
