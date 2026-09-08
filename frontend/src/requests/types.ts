export interface WhatsAppDelivery { status: 'Pending' | 'Processing' | 'Sent' | 'Delivered' | 'Read' | 'Failed' | 'PermanentlyFailed' | 'Cancelled' | 'Skipped' }

export type RequestStatus = 'Open' | 'InProgress' | 'WaitingForResident' | 'WaitingForManager' | 'WaitingForThirdParty' | 'WaitingForResidentClosure' | 'Resolved' | 'Cancelled'
export type RequestPriority = 'Normal' | 'High' | 'Urgent'

export interface Category { id: string; condominiumId: string; name: string; description: string | null }
export interface RequestCategory { id: string; name: string }
export interface TargetUnit { id: string; identifier: string; block: string | null }
export type RequestUnitOption = TargetUnit
export interface Person { id: string; fullName: string; isManager?: boolean }

export interface RequestListItem {
  id: string
  protocol?: string
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
  whatsAppDelivery?: WhatsAppDelivery | null
  id: string
  previousStatus: RequestStatus | null
  newStatus: RequestStatus
  changedByUserId: string
  changedByFullName: string
  reason: string | null
  createdAt: string
  answerMessageId?: string | null
}

export interface RequestDetails extends RequestListItem {
  mainDescription?: string
  canManageInternalNotes?: boolean
  author: Person
  description: string
  statusHistory: StatusHistoryItem[]
  aiAnalysis: RequestAiAnalysis | null
  originalReport: OriginalReport | null
  residentReplyRequirement: ResidentReplyRequirement | null
  residentClosureProposal?: ResidentClosureProposal | null
  residentSummary?: ResidentSummary | null
  agendaReminder?: AgendaReminderSummary | null
  hasUnreadResidentReply?: boolean
  hasUnreadResidentUpdate?: boolean
  serviceProvider?: ServiceProvider | null
  serviceProviderHistory?: ServiceProviderHistoryItem[]
  internalNotes?: RequestInternalNoteSummary[] | null
}
export interface ServiceProvider { id:string; name:string; companyName:string|null; specialty:string; specialties?:string[]; phone:string; isActive:boolean }
export interface ServiceProviderHistoryItem { id:string; eventType:'Linked'|'Changed'|'Removed'; previousName?:string|null; previousSpecialty?:string|null; providerName?:string|null; providerSpecialty?:string|null; changedByFullName:string; createdAt:string }

export interface AgendaReminderSummary { id: string; title: string; nextOccurrenceAtUtc: string | null; recurrenceType: 'None' | 'Weekly' | 'Monthly'; isActive: boolean; completedAt: string | null }

export interface ResidentSummary {
  fullName: string
  block: string | null
  unit: string | null
  phoneNumber: string | null
  email: string | null
  relationship: 'Owner' | 'Tenant' | 'AuthorizedOccupant' | null
}

export interface ResidentReplyRequirement { id: string; question: string; requestedAt: string; isActive: boolean }
export interface ResidentClosureProposal { conclusion: string; requestedAt: string; expiresAt: string }

export interface RequestAiAnalysis {
  title: string
  description: string
  suggestedCategory: string | null
  confidence: number | null
  missingInformation: string[]
  generatedAt: string
  model: string | null
}

export interface OriginalReport {
  text: string | null
  channel: 'WhatsApp'
  createdAt: string
  audioAttachment: OriginalAudioAttachment | null
}

export interface OriginalAudioAttachment {
  id: string
  originalFileName: string
  contentType: string
  fileSize: number
  contentUrl: string
}

export interface RequestMessage {
  whatsAppDelivery?: WhatsAppDelivery | null
  id: string
  requestId: string
  author: Person
  content: string
  channel?: 'Portal' | 'WhatsApp' | 'WhatsAppResidentUpdate' | 'System'
  createdAt: string
  isResidentReply?: boolean
  isAdministrativeEvent?: boolean
}
export interface RequestInternalNoteSummary { id: string; content: string; author: Person; createdAt: string; updatedAt: string | null }

export interface RequestAttachment {
  id: string
  requestId: string
  requestMessageId?: string | null
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
  hasUnreadResidentReply?: boolean
  hasUnreadResidentUpdate?: boolean
}

export interface RequestCounts {
  open: number
  inProgress: number
  waitingForResident: number
  waitingForManager: number
  waitingForThirdParty: number
  waitingForResidentClosure: number
  resolved: number
  cancelled: number
}

export interface ManagementRequestsResponse {
  total: number
  page: number
  pageSize: number
  counts: RequestCounts
  items: ManagementRequestItem[]
}
