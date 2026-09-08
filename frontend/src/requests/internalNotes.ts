import { api } from '../services/api'
import type { Person } from './types'

export interface RequestInternalNote {
  id: string
  content: string
  author: Person
  createdAt: string
  updatedAt: string | null
}

const path = (requestId: string) => `/management/requests/${requestId}/internal-notes`
export async function listInternalNotes(requestId: string) {
  return (await api.get<RequestInternalNote[]>(path(requestId))).data
}
export async function createInternalNote(requestId: string, content: string) {
  return (await api.post<RequestInternalNote>(path(requestId), { content })).data
}
export async function editInternalNote(requestId: string, noteId: string, content: string) {
  return (await api.put<RequestInternalNote>(`${path(requestId)}/${noteId}`, { content })).data
}
export async function deleteInternalNote(requestId: string, noteId: string) {
  await api.delete(`${path(requestId)}/${noteId}`)
}
