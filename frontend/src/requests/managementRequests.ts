import type { ManagementRequestItem, RequestPriority, RequestStatus } from './types'

const closedStatuses: RequestStatus[] = ['Resolved', 'Cancelled']

export function selectManagementRequests(
  items: ManagementRequestItem[],
  status: RequestStatus | '',
  search: string,
) {
  const normalizedSearch = search.trim().toLocaleLowerCase('pt-BR')

  return items.filter((request) => {
    if (!status && closedStatuses.includes(request.status)) return false
    if (status && request.status !== status) return false
    if (!normalizedSearch) return true

    return [request.title, request.author.fullName, request.category.name,
      request.targetUnit?.identifier, request.targetUnit?.block]
      .some((value) => value?.toLocaleLowerCase('pt-BR').includes(normalizedSearch))
  })
}

export function applySummaryFilter(status: RequestStatus, search: string) {
  return { status, priority: '' as RequestPriority | '', search }
}
