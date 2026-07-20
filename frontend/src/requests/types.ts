export type RequestStatus = 'Open' | 'InProgress' | 'WaitingForResident' | 'WaitingForThirdParty' | 'Resolved' | 'Cancelled'
export type RequestPriority = 'Normal' | 'High' | 'Urgent'

export interface Category { id: string; condominiumId: string; name: string; description: string | null }
export interface RequestCategory { id: string; name: string }
export interface TargetUnit { id: string; identifier: string; block: string | null }
export interface Person { id: string; fullName: string; isManager?: boolean }

export interface RequestListItem {
  id: string
  condominiumId: string
  category: RequestCategory
  targetUnit: TargetUnit | null
  title: string
  status: RequestStatus
  priority: RequestPriority
  createdAt: string
  updatedAt: string
  resolvedAt: string | null
}

export interface StatusHistoryItem {
  id: string
  previousStatus: RequestStatus | null
  newStatus: RequestStatus
  changedByUserId: string
  changedByFullName: string
  reason: string | null
  createdAt: string
}

export interface RequestDetails extends RequestListItem {
  author: Person
  description: string
  statusHistory: StatusHistoryItem[]
}

export interface RequestMessage {
  id: string
  requestId: string
  author: Person
  content: string
  createdAt: string
}

export interface RequestAttachment {
  id: string
  requestId: string
  originalFileName: string
  contentType: string
  fileSize: number
  uploadedBy: Person
  createdAt: string
  contentUrl: string
}

export interface CreatedRequest {
  id: string
  condominiumId: string
  authorUserId: string
  targetUnitId: string | null
  categoryId: string
  title: string
  description: string
  status: RequestStatus
  priority: RequestPriority
  createdAt: string
  updatedAt: string
  resolvedAt: string | null
}

export interface ManagementRequestItem extends RequestListItem {
  author: Person
  condominiumName: string
}

export interface RequestCounts {
  open: number
  inProgress: number
  waitingForResident: number
  waitingForThirdParty: number
  resolved: number
  cancelled: number
}

export interface ManagementRequestsResponse {
  total: number
  counts: RequestCounts
  items: ManagementRequestItem[]
}
