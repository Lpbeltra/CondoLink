import type { RequestStatus } from './types'

export function isClosedRequest(status: RequestStatus) {
  return status === 'Resolved' || status === 'Cancelled'
}

export function getRequestActionVisibility(status: RequestStatus) {
  const closed = isClosedRequest(status)
  const awaitingClosure = status === 'WaitingForResidentClosure'
  return { reopen: closed, changeStatus: !closed, changePriority: !closed, resolve: !closed && !awaitingClosure, cancel: !closed }
}

export const requestShortcutStatuses = { resolve: 'Resolved', cancel: 'Cancelled' } as const

export function getStatusConfirmation(status: RequestStatus) {
  if (status === 'Resolved') return 'A conclusão será enviada ao morador para confirmação.'
  if (status === 'Cancelled') return 'Deseja cancelar esta solicitação?'
  return null
}

export function canSubmitStatus(nextStatus: RequestStatus | '', isSaving: boolean): nextStatus is RequestStatus {
  return Boolean(nextStatus) && !isSaving
}
