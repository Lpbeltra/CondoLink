import axios from 'axios'
import { api } from '../services/api'

export const MAXIMUM_DOCUMENT_FILE_MEGABYTES = 25
export const MAXIMUM_DOCUMENT_FILE_BYTES = MAXIMUM_DOCUMENT_FILE_MEGABYTES * 1024 * 1024
export const DOCUMENT_FILE_TOO_LARGE_MESSAGE = `O arquivo excede o limite de ${MAXIMUM_DOCUMENT_FILE_MEGABYTES} MB.`

interface DocumentUploadError { code?: string; message?: string; error?: string }

export function getDocumentUploadError(error: unknown) {
  if (axios.isAxiosError<DocumentUploadError>(error)) {
    const message = error.response?.data?.message ?? error.response?.data?.error
    if (message) return message
    if (!error.response) return 'Não foi possível conectar ao servidor. Tente novamente.'
  }
  return 'Não foi possível enviar o documento. Tente novamente.'
}

export interface AssistantSource { documentId: string; documentName: string; pageNumber: number | null; sectionTitle: string | null; excerpt: string; marker: string }
export interface AssistantConversation { id: string; title: string; requestId: string | null; requestTitle: string | null; createdAt: string; updatedAt: string }
export interface AssistantMessage { id: string; role: 'User' | 'Assistant'; content: string; createdAt: string; sources: { source: AssistantSource; documentExists?: boolean; documentCurrentlyActive: boolean }[] }
export interface ConversationDetails { conversation: AssistantConversation; messages: AssistantMessage[]; requestContext: { id: string; title: string } | null; contextUnavailable: boolean }
export interface AssistantDocument { id: string; name: string; documentType: string; originalFileName: string; version: number; isActive: boolean; processingStatus: string; processingError: string | null }
export const listDocuments = async (condominiumId: string) => (await api.get<AssistantDocument[]>(`/condominiums/${condominiumId}/documents`)).data
export const uploadDocument = async (condominiumId: string, form: FormData) => (await api.post(`/condominiums/${condominiumId}/documents`, form, { timeout: 5 * 60 * 1000 })).data
export const setDocumentActive = async (condominiumId: string, id: string, active: boolean) => api.put(`/condominiums/${condominiumId}/documents/${id}/active`, { active })
export const deleteDocument = async (condominiumId: string, id: string) => api.delete(`/condominiums/${condominiumId}/documents/${id}`)
export const createConversation = async (condominiumId: string, requestId?: string) => (await api.post(`/condominiums/${condominiumId}/assistant/conversations`, { requestId, title: requestId ? 'Consulta sobre solicitação' : 'Nova conversa' })).data as { id: string; requestId: string | null }
export const startConversation = async (condominiumId: string, question: string, requestId?: string) => (await api.post(`/condominiums/${condominiumId}/assistant/messages`, { question, requestId })).data as { conversation: AssistantConversation; answer: string; sources: AssistantSource[] }
export const listConversations = async (condominiumId: string, page = 1, search = '') => (await api.get(`/condominiums/${condominiumId}/assistant/conversations`, { params: { page, pageSize: 20, search: search || undefined } })).data as { items: AssistantConversation[]; hasMore: boolean; total: number }
export const getConversation = async (condominiumId: string, id: string) => (await api.get<ConversationDetails>(`/condominiums/${condominiumId}/assistant/conversations/${id}`)).data
export const askAssistant = async (condominiumId: string, conversationId: string, question: string) => (await api.post(`/condominiums/${condominiumId}/assistant/conversations/${conversationId}/messages`, { question })).data as { answer: string; sources: AssistantSource[] }
export const removeRequestContext = async (condominiumId: string, conversationId: string) => api.delete(`/condominiums/${condominiumId}/assistant/conversations/${conversationId}/request-context`)
export const deleteConversation = async (condominiumId: string, conversationId: string) => api.delete(`/condominiums/${condominiumId}/assistant/conversations/${conversationId}`)
