import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { PayslipDistribution } from './PayslipDistribution'

const listBatches = vi.fn()
const getBatch = vi.fn()
const uploadBatch = vi.fn()
const updateDocumentAssociation = vi.fn()
const confirmBatch = vi.fn()
const getDistributionSummary = vi.fn()
const distributeBatch = vi.fn()
const listDeliveries = vi.fn()
const resendDocument = vi.fn()
const previewDocumentUrl = vi.fn()

vi.mock('./api', () => ({
  listBatches: (...args: unknown[]) => listBatches(...args),
  getBatch: (...args: unknown[]) => getBatch(...args),
  uploadBatch: (...args: unknown[]) => uploadBatch(...args),
  updateDocumentAssociation: (...args: unknown[]) => updateDocumentAssociation(...args),
  confirmBatch: (...args: unknown[]) => confirmBatch(...args),
  getDistributionSummary: (...args: unknown[]) => getDistributionSummary(...args),
  distributeBatch: (...args: unknown[]) => distributeBatch(...args),
  listDeliveries: (...args: unknown[]) => listDeliveries(...args),
  resendDocument: (...args: unknown[]) => resendDocument(...args),
  previewDocumentUrl: (...args: unknown[]) => previewDocumentUrl(...args),
}))
vi.mock('../employees/api', () => ({
  listEmployees: () => Promise.resolve([{ id: 'emp-1', fullName: 'João da Silva' }, { id: 'emp-2', fullName: 'Maria Souza' }]),
}))

const batch = {
  id: 'batch-1', condominiumId: 'c1', documentType: 'Payslip', competenceMonth: 8, competenceYear: 2026,
  status: 'ReadyForReview', createdAt: '', createdByName: 'Síndico', confirmedAt: null, confirmedByName: null,
  failureReason: null, documentCount: 2, identifiedCount: 1, needsReviewCount: 1, unidentifiedCount: 0,
  ignoredCount: 0, confirmedCount: 0,
}

const identifiedDocument = {
  id: 'doc-1', batchId: 'batch-1', employeeId: 'emp-1', employeeName: 'João da Silva', documentType: 'Payslip',
  competenceMonth: 8, competenceYear: 2026, originalFileName: 'folha.pdf', pageStart: 1, pageEnd: 1,
  identificationStatus: 'Identified', identificationConfidence: 'High', identificationMethod: 'RegistrationNumber',
  createdAt: '', updatedAt: '', confirmedAt: null, possibleDuplicate: false,
}
const needsReviewDocument = {
  ...identifiedDocument, id: 'doc-2', employeeId: null, employeeName: null,
  identificationStatus: 'NeedsReview', identificationConfidence: 'Low', identificationMethod: 'FuzzyName',
}

describe('PayslipDistribution', () => {
  beforeEach(() => {
    listBatches.mockReset(); getBatch.mockReset(); uploadBatch.mockReset(); updateDocumentAssociation.mockReset()
    confirmBatch.mockReset(); getDistributionSummary.mockReset(); distributeBatch.mockReset()
    listDeliveries.mockReset(); resendDocument.mockReset(); previewDocumentUrl.mockReset()
  })

  it('shows batch history and opens a batch for review', async () => {
    listBatches.mockResolvedValue([batch])
    getBatch.mockResolvedValue({ batch, documents: [identifiedDocument, needsReviewDocument] })
    render(<PayslipDistribution condominiumId="c1" />)
    expect(await screen.findByText('Agosto/2026')).toBeInTheDocument()
    await userEvent.setup().click(screen.getByRole('button', { name: 'Abrir' }))
    expect(await screen.findByText(/Holerites — Agosto\/2026/)).toBeInTheDocument()
  })

  it('shows a Completed batch as "Concluído" and opens it straight to the distribution view', async () => {
    const completedBatch = { ...batch, status: 'Completed' }
    listBatches.mockResolvedValue([completedBatch])
    getDistributionSummary.mockResolvedValue({ batchId: 'batch-1', totalConfirmed: 2, ready: 0, noPhone: 0, invalidPhone: 0, alreadyQueuedOrSent: 2 })
    listDeliveries.mockResolvedValue([
      { employeeDocumentId: 'doc-1', employeeId: 'emp-1', employeeName: 'João da Silva', status: 'Delivered', attemptCount: 1, queuedAt: '', sentAt: '2026-08-01T10:00:00Z', deliveredAt: '2026-08-01T10:01:00Z', readAt: null, failedAt: null, lastErrorCode: null, lastErrorDescription: null },
    ])
    render(<PayslipDistribution condominiumId="c1" />)
    expect(await screen.findByText('Concluído')).toBeInTheDocument()
    await userEvent.setup().click(screen.getByRole('button', { name: 'Abrir' }))
    // Completed never claims "100% delivered" — individual delivery status stays visible.
    expect(await screen.findByText('João da Silva')).toBeInTheDocument()
    expect(getBatch).not.toHaveBeenCalled() // routed straight to distribution view, not the review screen.
  })

  it('uploads a new batch and moves straight to review', async () => {
    listBatches.mockResolvedValue([])
    uploadBatch.mockResolvedValue({ id: 'batch-2', status: 'Uploaded' })
    getBatch.mockResolvedValue({ batch: { ...batch, id: 'batch-2', status: 'Processing' }, documents: [] })
    render(<PayslipDistribution condominiumId="c1" />)
    await screen.findByText('Nenhum lote de holerites ainda.')
    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: 'Distribuir holerites' }))
    const file = new File(['%PDF-1.4'], 'folha.pdf', { type: 'application/pdf' })
    const input = document.querySelector('input[type="file"]') as HTMLInputElement
    await user.upload(input, file)
    await user.click(screen.getByRole('button', { name: 'Enviar e processar' }))
    await waitFor(() => expect(uploadBatch).toHaveBeenCalledWith('c1', [file], expect.any(Number), expect.any(Number)))
    expect(await screen.findByText('Processando documentos enviados… isso pode levar alguns instantes.')).toBeInTheDocument()
  })

  it('disables batch confirmation while any document is still pending review', async () => {
    listBatches.mockResolvedValue([batch])
    getBatch.mockResolvedValue({ batch, documents: [identifiedDocument, needsReviewDocument] })
    render(<PayslipDistribution condominiumId="c1" />)
    await userEvent.setup().click(await screen.findByRole('button', { name: 'Abrir' }))
    await screen.findByText('João da Silva')
    expect(screen.getByRole('button', { name: 'Confirmar associações' })).toBeDisabled()
  })

  it('enables batch confirmation once every document is confirmed or ignored', async () => {
    listBatches.mockResolvedValue([batch])
    getBatch.mockResolvedValue({
      batch, documents: [
        { ...identifiedDocument, identificationStatus: 'Confirmed' },
        { ...needsReviewDocument, identificationStatus: 'Ignored' },
      ],
    })
    render(<PayslipDistribution condominiumId="c1" />)
    await userEvent.setup().click(await screen.findByRole('button', { name: 'Abrir' }))
    await waitFor(() => expect(screen.getByRole('button', { name: 'Confirmar associações' })).not.toBeDisabled())
  })

  it('assigning and confirming a document calls the API with that document id', async () => {
    listBatches.mockResolvedValue([batch])
    getBatch.mockResolvedValue({ batch, documents: [identifiedDocument, needsReviewDocument] })
    render(<PayslipDistribution condominiumId="c1" />)
    await userEvent.setup().click(await screen.findByRole('button', { name: 'Abrir' }))
    await screen.findByText('João da Silva')

    const user = userEvent.setup()
    await user.click(screen.getAllByRole('button', { name: 'Confirmar' })[0])
    await waitFor(() => expect(updateDocumentAssociation).toHaveBeenCalledWith('c1', 'doc-1', 'Confirm', undefined))

    await user.click(screen.getAllByRole('button', { name: 'Ignorar' })[1])
    await waitFor(() => expect(updateDocumentAssociation).toHaveBeenCalledWith('c1', 'doc-2', 'Ignore', undefined))
  })

  it('confirming the batch calls confirmBatch and moves to distribution', async () => {
    const allConfirmed = [
      { ...identifiedDocument, identificationStatus: 'Confirmed' },
      { ...needsReviewDocument, identificationStatus: 'Ignored' },
    ]
    listBatches.mockResolvedValue([batch])
    getBatch.mockResolvedValue({ batch, documents: allConfirmed })
    confirmBatch.mockResolvedValue(undefined)
    getDistributionSummary.mockResolvedValue({ batchId: 'batch-1', totalConfirmed: 1, ready: 1, noPhone: 0, invalidPhone: 0, alreadyQueuedOrSent: 0 })
    listDeliveries.mockResolvedValue([])
    render(<PayslipDistribution condominiumId="c1" />)
    await userEvent.setup().click(await screen.findByRole('button', { name: 'Abrir' }))
    const confirmButton = await screen.findByRole('button', { name: 'Confirmar associações' })
    await waitFor(() => expect(confirmButton).not.toBeDisabled())
    await userEvent.setup().click(confirmButton)
    await waitFor(() => expect(confirmBatch).toHaveBeenCalledWith('c1', 'batch-1'))
    expect(await screen.findByText('Enviar 1 holerite')).toBeInTheDocument()
  })

  it('requires explicit confirmation before dispatching WhatsApp sends', async () => {
    listBatches.mockResolvedValue([{ ...batch, status: 'Confirmed' }])
    getDistributionSummary.mockResolvedValue({ batchId: 'batch-1', totalConfirmed: 3, ready: 3, noPhone: 0, invalidPhone: 0, alreadyQueuedOrSent: 0 })
    listDeliveries.mockResolvedValue([])
    render(<PayslipDistribution condominiumId="c1" />)
    await userEvent.setup().click(await screen.findByRole('button', { name: 'Abrir' }))
    const sendButton = await screen.findByRole('button', { name: 'Enviar 3 holerites' })
    const user = userEvent.setup()
    await user.click(sendButton)
    expect(await screen.findByText('Enviar 3 holerites via WhatsApp?')).toBeInTheDocument()
    expect(distributeBatch).not.toHaveBeenCalled()
    await user.click(screen.getByRole('button', { name: 'Confirmar envio' }))
    await waitFor(() => expect(distributeBatch).toHaveBeenCalledWith('c1', 'batch-1'))
  })

  it('offers resend only for failed deliveries', async () => {
    listBatches.mockResolvedValue([{ ...batch, status: 'Distributing' }])
    getDistributionSummary.mockResolvedValue({ batchId: 'batch-1', totalConfirmed: 2, ready: 0, noPhone: 0, invalidPhone: 0, alreadyQueuedOrSent: 2 })
    listDeliveries.mockResolvedValue([
      { employeeDocumentId: 'doc-1', employeeId: 'emp-1', employeeName: 'João da Silva', status: 'Delivered', attemptCount: 1, queuedAt: '', sentAt: '2026-08-01T10:00:00Z', deliveredAt: '2026-08-01T10:01:00Z', readAt: null, failedAt: null, lastErrorCode: null, lastErrorDescription: null },
      { employeeDocumentId: 'doc-2', employeeId: 'emp-2', employeeName: 'Maria Souza', status: 'Failed', attemptCount: 1, queuedAt: '', sentAt: null, deliveredAt: null, readAt: null, failedAt: '2026-08-01T10:00:00Z', lastErrorCode: 'invalid_number', lastErrorDescription: 'Número inválido' },
    ])
    render(<PayslipDistribution condominiumId="c1" />)
    await userEvent.setup().click(await screen.findByRole('button', { name: 'Abrir' }))
    await screen.findByText('Maria Souza')
    expect(screen.queryAllByRole('button', { name: 'Reenviar' })).toHaveLength(1)
    await userEvent.setup().click(screen.getByRole('button', { name: 'Reenviar' }))
    await waitFor(() => expect(resendDocument).toHaveBeenCalledWith('c1', 'doc-2'))
  })
})
