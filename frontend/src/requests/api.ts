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
}) {
  return (await api.get<ManagementRequestsResponse>(
    '/management/requests',
    { params: filters },
  )).data
}

export async function updateRequestStatus(requestId: string, status: RequestStatus, reason: string | null) {
  return (await api.patch(`/requests/${requestId}/status`, { status, reason })).data
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
