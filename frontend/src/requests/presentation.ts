import axios from 'axios'
import type { RequestListItem, RequestPriority, RequestStatus } from './types'

export const statusPresentation: Record<RequestStatus, { label: string; color: 'info' | 'warning' | 'secondary' | 'success' | 'error' | 'default' }> = {
  Open: { label: 'Aberta', color: 'info' },
  InProgress: { label: 'Em andamento', color: 'info' },
  WaitingForResident: { label: 'Aguardando morador', color: 'warning' },
  WaitingForManager: { label: 'Dar andamento', color: 'secondary' },
  WaitingForThirdParty: { label: 'Aguardando terceiro', color: 'default' },
  WaitingForResidentClosure: { label: 'Conclusão aguardando confirmação', color: 'warning' },
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
export function formatRequestProtocol(id: string, protocol?: string) {
  return (protocol || id.replace(/-/g, '').slice(0, 8)).toUpperCase()
}
export function formatDateTime(value: string) { return dateTimeFormatter.format(new Date(value)).replace(',', ' às') }

export function formatResidentPhone(value: string | null | undefined) {
  if (!value?.trim()) return 'Não informado'
  const raw = value.trim()
  const digits = raw.replace(/\D/g, '')
  if (!raw.startsWith('+') && digits.length === 11)
    return `+55 (${digits.slice(0, 2)}) ${digits.slice(2, 7)}-${digits.slice(7)}`
  if (!raw.startsWith('+') && digits.length === 10)
    return `+55 (${digits.slice(0, 2)}) ${digits.slice(2, 6)}-${digits.slice(6)}`
  if ((raw.startsWith('+55') || !raw.startsWith('+')) && digits.length === 13
      && digits.startsWith('55'))
    return `+55 (${digits.slice(2, 4)}) ${digits.slice(4, 9)}-${digits.slice(9)}`
  if ((raw.startsWith('+55') || !raw.startsWith('+')) && digits.length === 12
      && digits.startsWith('55'))
    return `+55 (${digits.slice(2, 4)}) ${digits.slice(4, 8)}-${digits.slice(8)}`
  return raw
}

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
  return status !== 'Cancelled' && status !== 'Resolved'
}

export function isClosedRequest(status: RequestStatus) {
  return status === 'Resolved' || status === 'Cancelled'
}

export const allowedStatusTransitions: Record<RequestStatus, RequestStatus[]> = {
  Open: ['InProgress', 'WaitingForResidentClosure', 'Resolved', 'Cancelled'],
  InProgress: ['WaitingForResident', 'WaitingForManager', 'WaitingForThirdParty', 'WaitingForResidentClosure', 'Resolved', 'Cancelled'],
  WaitingForResident: ['InProgress', 'WaitingForResidentClosure', 'Resolved', 'Cancelled'],
  WaitingForThirdParty: ['InProgress', 'WaitingForResidentClosure', 'Resolved', 'Cancelled'],
  WaitingForManager: ['InProgress', 'WaitingForResidentClosure', 'Resolved', 'Cancelled'],
  WaitingForResidentClosure: ['InProgress', 'Cancelled'],
  Resolved: ['Open'],
  Cancelled: ['Open'],
}

export function getRequestError(error: unknown, fallback = 'Não foi possível carregar as informações.') {
  if (axios.isAxiosError(error)) {
    if (error.response?.status === 403) return 'Você não possui acesso a esta solicitação.'
    if (error.response?.status === 404) return 'Solicitação não encontrada.'
    if (!error.response || error.response.status >= 500) return fallback
  }
  return 'Não foi possível concluir esta ação. Tente novamente.'
}
