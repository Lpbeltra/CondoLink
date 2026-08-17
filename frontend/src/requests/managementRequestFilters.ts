import type { RequestPriority, RequestStatus } from './types'
import type {
  ManagementRequestSort,
  SortDirection,
} from './managementRequests'

const statuses: RequestStatus[] = [
  'Open',
  'InProgress',
  'WaitingForResident',
  'WaitingForManager',
  'WaitingForThirdParty',
  'WaitingForResidentClosure',
  'Resolved',
  'Cancelled',
]
const priorities: RequestPriority[] = ['Normal', 'High', 'Urgent']
const sorts: ManagementRequestSort[] = [
  'createdAt',
  'priority',
  'condominium',
]
const directions: SortDirection[] = ['asc', 'desc']

export interface ManagementRequestFilters {
  status: RequestStatus | ''
  priority: RequestPriority | ''
  categoryId: string
  search: string
  sort: ManagementRequestSort
  direction: SortDirection
  condominiumId: string
}

export function parseManagementRequestFilters(
  params: URLSearchParams,
): ManagementRequestFilters {
  const status = params.get('status')
  const priority = params.get('priority')
  const sort = params.get('sort')
  const direction = params.get('direction')
  return {
    status: statuses.includes(status as RequestStatus)
      ? status as RequestStatus
      : '',
    priority: priorities.includes(priority as RequestPriority)
      ? priority as RequestPriority
      : '',
    categoryId: params.get('categoryId') ?? '',
    search: params.get('search') ?? '',
    sort: sorts.includes(sort as ManagementRequestSort)
      ? sort as ManagementRequestSort
      : 'createdAt',
    direction: directions.includes(direction as SortDirection)
      ? direction as SortDirection
      : 'desc',
    condominiumId: params.get('condominiumId') ?? '',
  }
}

export function setManagementRequestFilter(
  params: URLSearchParams,
  key: keyof ManagementRequestFilters,
  value: string,
) {
  const next = new URLSearchParams(params)
  if (value) next.set(key, value)
  else next.delete(key)
  return next
}

export function clearManagementRequestFilters(params: URLSearchParams) {
  const next = new URLSearchParams()
  const condominiumId = params.get('condominiumId')
  if (condominiumId) next.set('condominiumId', condominiumId)
  return next
}

export function syncCondominiumFilter(
  params: URLSearchParams,
  condominiumId: string | null,
) {
  const next = new URLSearchParams(params)
  const previous = next.get('condominiumId')
  if (condominiumId) next.set('condominiumId', condominiumId)
  else next.delete('condominiumId')
  if (previous !== condominiumId) next.delete('categoryId')
  return next
}
