import { describe, expect, it, vi } from 'vitest'

const request = vi.hoisted(() => ({ get: vi.fn(), post: vi.fn(), patch: vi.fn(), put: vi.fn() }))
vi.mock('../services/api', () => ({ api: request }))

import { confirmBatch, distributeBatch, getBatch, getDistributionSummary, listBatches, listDeliveries, previewDocumentUrl, reopenBatch, replaceDocumentFile, resendDocument, updateDocumentAssociation, uploadBatch } from './api'

describe('administrator EmployeeDocuments API', () => {
  it('uses only batch-scoped administrator routes for every operation', async () => {
    request.get.mockResolvedValue({ data: [] })
    request.post.mockResolvedValue({ data: { id: 'batch-1', status: 'Uploaded' } })
    request.patch.mockResolvedValue({ data: {} })
    request.put.mockResolvedValue({ data: {} })
    vi.stubGlobal('URL', { createObjectURL: vi.fn(() => 'blob:test') })

    await listBatches()
    await getBatch('batch-1')
    await uploadBatch([new File(['pdf'], 'a.pdf')], 8, 2026, ['employee-a', 'employee-b'])
    await updateDocumentAssociation('batch-1', 'document-1', 'Assign', 'employee-a')
    await replaceDocumentFile('batch-1', 'document-1', new File(['pdf'], 'replacement.pdf'))
    await confirmBatch('batch-1')
    await reopenBatch('batch-1')
    await getDistributionSummary('batch-1')
    await distributeBatch('batch-1')
    await listDeliveries('batch-1')
    await resendDocument('batch-1', 'document-1')
    await previewDocumentUrl('batch-1', 'document-1')

    const urls = [...request.get.mock.calls, ...request.post.mock.calls, ...request.patch.mock.calls, ...request.put.mock.calls]
      .map(([url]) => String(url))
    expect(urls.every(url => url.startsWith('/administrator/employees/documents/batches'))).toBe(true)
    expect(urls.some(url => url.includes('/condominiums/'))).toBe(false)
  })
})
