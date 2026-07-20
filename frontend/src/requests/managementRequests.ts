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

export type ManagementRequestSort = 'createdAt' | 'priority' | 'condominium'
export type SortDirection = 'asc' | 'desc'

const priorityOrder: Record<RequestPriority, number> = { Normal: 0, High: 1, Urgent: 2 }

export function sortManagementRequests(items: ManagementRequestItem[], sort: ManagementRequestSort, direction: SortDirection) {
  const multiplier = direction === 'asc' ? 1 : -1
  return [...items].sort((left, right) => {
    const comparison = sort === 'createdAt'
      ? new Date(left.createdAt).getTime() - new Date(right.createdAt).getTime()
      : sort === 'priority'
        ? priorityOrder[left.priority] - priorityOrder[right.priority]
        : left.condominiumName.localeCompare(right.condominiumName, 'pt-BR')
    return comparison === 0 ? left.id.localeCompare(right.id) : comparison * multiplier
  })
}
