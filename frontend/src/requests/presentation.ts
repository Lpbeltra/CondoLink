import axios from 'axios'
import type { RequestListItem, RequestPriority, RequestStatus } from './types'

export const statusPresentation: Record<RequestStatus, { label: string; color: 'info' | 'warning' | 'secondary' | 'success' | 'default' }> = {
  Open: { label: 'Aberta', color: 'info' },
  InProgress: { label: 'Em andamento', color: 'secondary' },
  WaitingForResident: { label: 'Aguardando você', color: 'warning' },
  WaitingForThirdParty: { label: 'Aguardando terceiro', color: 'warning' },
  Resolved: { label: 'Resolvida', color: 'success' },
  Cancelled: { label: 'Cancelada', color: 'default' },
}

export const priorityPresentation: Record<RequestPriority, { label: string; color: 'default' | 'warning' | 'error' }> = {
  Normal: { label: 'Normal', color: 'default' },
  High: { label: 'Alta', color: 'warning' },
  Urgent: { label: 'Urgente', color: 'error' },
}

const dateFormatter = new Intl.DateTimeFormat('pt-BR', { day: '2-digit', month: 'short', year: 'numeric' })
const dateTimeFormatter = new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' })

export function formatDate(value: string) { return dateFormatter.format(new Date(value)) }
export function formatDateTime(value: string) { return dateTimeFormatter.format(new Date(value)).replace(',', ' às') }

export function formatRelativeDate(value: string, now = new Date()) {
  const minutes = Math.max(0, Math.floor((now.getTime() - new Date(value).getTime()) / 60_000))
  if (minutes < 1) return 'Atualizada agora'
  if (minutes < 60) return `Atualizada há ${minutes} min`
  const hours = Math.floor(minutes / 60)
  if (hours < 24) return `Atualizada há ${hours}h`
  return `Atualizada em ${formatDate(value)}`
}

export function filterRequestsByCondominium(requests: RequestListItem[], condominiumId: string) {
  return requests.filter((request) => request.condominiumId === condominiumId)
}

export function canSendMessage(status: RequestStatus) {
  return status !== 'Cancelled'
}

export const allowedStatusTransitions: Record<RequestStatus, RequestStatus[]> = {
  Open: ['InProgress', 'Cancelled'],
  InProgress: ['WaitingForResident', 'WaitingForThirdParty', 'Resolved', 'Cancelled'],
  WaitingForResident: ['InProgress', 'Resolved', 'Cancelled'],
  WaitingForThirdParty: ['InProgress', 'Resolved', 'Cancelled'],
  Resolved: ['InProgress'],
  Cancelled: [],
}

export function getRequestError(error: unknown, fallback = 'Não foi possível carregar as informações.') {
  if (axios.isAxiosError(error)) {
    if (error.response?.status === 403) return 'Você não possui acesso a esta solicitação.'
    if (error.response?.status === 404) return 'Solicitação não encontrada.'
    if (!error.response || error.response.status >= 500) return fallback
  }
  return 'Não foi possível concluir esta ação. Tente novamente.'
}
