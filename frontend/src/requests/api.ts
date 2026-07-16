import { api } from '../services/api'
import type { Category, CreatedRequest, RequestDetails, RequestListItem, RequestMessage } from './types'

export async function listMyRequests() {
  return (await api.get<RequestListItem[]>('/requests/mine')).data
}

export async function listCategories(condominiumId: string) {
  return (await api.get<Category[]>(`/condominiums/${condominiumId}/categories`)).data
}

export async function createRequest(condominiumId: string, payload: { categoryId: string; title: string; description: string }) {
  return (await api.post<CreatedRequest>(`/condominiums/${condominiumId}/requests`, { ...payload, targetUnitId: null })).data
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
