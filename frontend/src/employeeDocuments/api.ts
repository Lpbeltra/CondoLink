import { api } from '../services/api'

export interface EmployeeDocumentBatch {
  id: string
  condominiumId: string
  documentType: string
  competenceMonth: number
  competenceYear: number
  status: string
  createdAt: string
  createdByName: string
  confirmedAt: string | null
  confirmedByName: string | null
  failureReason: string | null
  documentCount: number
  identifiedCount: number
  needsReviewCount: number
  unidentifiedCount: number
  ignoredCount: number
  confirmedCount: number
}

export interface EmployeeDocument {
  id: string
  batchId: string
  employeeId: string | null
  employeeName: string | null
  documentType: string
  competenceMonth: number
  competenceYear: number
  originalFileName: string
  pageStart: number
  pageEnd: number
  identificationStatus: 'Unidentified' | 'NeedsReview' | 'Identified' | 'Confirmed' | 'Ignored'
  identificationConfidence: 'None' | 'Low' | 'Medium' | 'High'
  identificationMethod: string
  createdAt: string
  updatedAt: string
  confirmedAt: string | null
  possibleDuplicate: boolean
}

export interface EmployeeDocumentBatchDetail {
  batch: EmployeeDocumentBatch
  documents: EmployeeDocument[]
}

export interface DistributionSummary {
  batchId: string
  totalConfirmed: number
  ready: number
  noPhone: number
  invalidPhone: number
  alreadyQueuedOrSent: number
}

export interface EmployeeDocumentDelivery {
  employeeDocumentId: string
  employeeId: string
  employeeName: string
  status: string
  attemptCount: number
  queuedAt: string
  sentAt: string | null
  deliveredAt: string | null
  readAt: string | null
  failedAt: string | null
  lastErrorCode: string | null
  lastErrorDescription: string | null
}

export async function listBatches(condominiumId: string) {
  return (await api.get<EmployeeDocumentBatch[]>(`/condominiums/${condominiumId}/employees/documents/batches`)).data
}

export async function getBatch(condominiumId: string, batchId: string) {
  return (await api.get<EmployeeDocumentBatchDetail>(
    `/condominiums/${condominiumId}/employees/documents/batches/${batchId}`)).data
}

export async function uploadBatch(condominiumId: string, files: File[], competenceMonth: number, competenceYear: number) {
  const form = new FormData()
  form.append('competenceMonth', String(competenceMonth))
  form.append('competenceYear', String(competenceYear))
  files.forEach(file => form.append('files', file))
  return (await api.post<{ id: string; status: string }>(
    `/condominiums/${condominiumId}/employees/documents/batches`, form)).data
}

export type DocumentAction = 'Assign' | 'Clear' | 'Ignore' | 'Confirm'

export async function updateDocumentAssociation(
  condominiumId: string, documentId: string, action: DocumentAction, employeeId?: string,
) {
  return (await api.patch<EmployeeDocument>(
    `/condominiums/${condominiumId}/employees/documents/${documentId}`,
    { action, employeeId: employeeId ?? null })).data
}

export async function confirmBatch(condominiumId: string, batchId: string) {
  await api.post(`/condominiums/${condominiumId}/employees/documents/batches/${batchId}/confirm`)
}

export async function getDistributionSummary(condominiumId: string, batchId: string) {
  return (await api.get<DistributionSummary>(
    `/condominiums/${condominiumId}/employees/documents/batches/${batchId}/distribution-summary`)).data
}

export async function distributeBatch(condominiumId: string, batchId: string) {
  return (await api.post<{ queued: number }>(
    `/condominiums/${condominiumId}/employees/documents/batches/${batchId}/distribute`)).data
}

export async function listDeliveries(condominiumId: string, batchId: string) {
  return (await api.get<EmployeeDocumentDelivery[]>(
    `/condominiums/${condominiumId}/employees/documents/batches/${batchId}/deliveries`)).data
}

export async function resendDocument(condominiumId: string, documentId: string) {
  await api.post(`/condominiums/${condominiumId}/employees/documents/${documentId}/resend`)
}

// Fetched as a blob and shown via an object URL — never a public URL, and
// always subject to the same authentication/authorization as every other call.
export async function previewDocumentUrl(condominiumId: string, documentId: string) {
  const response = await api.get(`/condominiums/${condominiumId}/employees/documents/${documentId}/preview`,
    { responseType: 'blob' })
  return URL.createObjectURL(response.data as Blob)
}
