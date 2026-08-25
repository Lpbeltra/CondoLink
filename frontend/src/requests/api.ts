import { api } from '../services/api'
import type { Category, CreatedRequest, ManagementRequestsResponse, RequestAttachment, RequestDetails, RequestListItem, RequestMessage, RequestPriority, RequestStatus, RequestUnitOption } from './types'

export async function listMyRequests() {
  return (await api.get<RequestListItem[]>('/requests/mine')).data
}

export async function listCategories(condominiumId: string) {
  return (await api.get<Category[]>(`/condominiums/${condominiumId}/categories`)).data
}

export async function listMyRequestUnits(condominiumId: string) {
  return (await api.get<RequestUnitOption[]>(
    `/condominiums/${condominiumId}/units/mine`,
  )).data
}

export async function createRequest(condominiumId: string, payload: { categoryId: string; targetUnitId: string | null; title: string; description: string }) {
  return (await api.post<CreatedRequest>(`/condominiums/${condominiumId}/requests`, payload)).data
}

export async function getRequest(requestId: string) {
  return (await api.get<RequestDetails>(`/requests/${requestId}`)).data
}

export async function acknowledgeResidentUpdate(requestId: string) {
  await api.post(`/requests/${requestId}/resident-update-acknowledgement`)
}

export async function listRequestMessages(requestId: string) {
  return (await api.get<RequestMessage[]>(`/requests/${requestId}/messages`)).data
}

export async function createRequestMessage(requestId: string, content: string) {
  return (await api.post<RequestMessage>(`/requests/${requestId}/messages`, { content })).data
}

export async function listManagementRequests(filters: {
  status?: RequestStatus
  priority?: RequestPriority
  condominiumId?: string
  search?: string
  page?: number
  pageSize?: number
}) {
  return (await api.get<ManagementRequestsResponse>(
    '/management/requests',
    { params: filters },
  )).data
}

export async function createResidentReply(requestId: string, message: string, files: File[], onProgress?: (loaded: number, total?: number) => void) {
  const form = new FormData()
  if (message.trim()) form.append('message', message.trim())
  files.forEach(file => form.append('files', file))
  return (await api.post<{ messageId: string; status: 'InProgress' }>(
    `/requests/${requestId}/resident-reply`, form,
    { timeout: 5 * 60 * 1000, onUploadProgress: event => onProgress?.(event.loaded, event.total) },
  )).data
}

export async function confirmResidentClosure(requestId: string) {
  return (await api.post<{ code: 'confirmed' }>(`/requests/${requestId}/resident-closure/confirm`)).data
}

export async function questionResidentClosure(requestId: string, message: string) {
  return (await api.post<{ code: 'questioned' }>(`/requests/${requestId}/resident-closure/question`, { message })).data
}

export async function updateRequestStatus(requestId: string, status: RequestStatus, reason: string | null) {
  return (await api.patch(`/requests/${requestId}/status`, { status, reason })).data
}

export async function suggestRequestStatusMessage(requestId: string, status: RequestStatus, message: string) {
  return (await api.post<{ suggestion: string }>(
    `/requests/${requestId}/status-message-suggestion`, { status, message },
  )).data
}

export async function createAdministrativeRequestUpdate(requestId: string, content: string) {
  return (await api.post(`/management/requests/${requestId}/updates`, { content })).data
}

export async function updateRequestPriority(requestId: string, priority: RequestPriority) {
  return (await api.patch(`/requests/${requestId}/priority`, { priority })).data
}

export async function listRequestAttachments(requestId: string) {
  return (await api.get<RequestAttachment[]>(`/requests/${requestId}/attachments`)).data
}

export async function uploadRequestAttachments(
  requestId: string,
  files: File[],
  onProgress?: (loaded: number, total?: number) => void,
) {
  const form = new FormData()
  files.forEach((file) => form.append('files', file))
  return (await api.post<RequestAttachment[]>(
    `/requests/${requestId}/attachments`,
    form,
    {
      timeout: 5 * 60 * 1000,
      onUploadProgress: event => onProgress?.(event.loaded, event.total),
    },
  )).data
}

export async function getRequestAttachmentBlob(contentUrl: string) {
  return (await api.get<Blob>(contentUrl, { responseType: 'blob' })).data
}

export async function deleteRequestAttachment(attachmentId: string) {
  await api.delete(`/request-attachments/${attachmentId}`)
}
