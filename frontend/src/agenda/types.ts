export type AgendaRecurrence = 'None' | 'Weekly' | 'Monthly'
export interface AgendaReminder {
  id: string; title: string; description: string | null; unitId: string | null
  unitIdentifier: string | null; block: string | null; relatedThirdParty: string | null
  startsAtUtc: string; nextOccurrenceAtUtc: string | null; timeZoneId: string
  recurrenceType: AgendaRecurrence; notifyByWhatsApp: boolean; notifyByEmail: boolean
  isActive: boolean; completedAt: string | null; createdAt: string
  requestCount: number; requestIds: string[]; linkedRequests: AgendaRequestLink[]
}
export interface AgendaRequestLink { id: string; protocol: string; title: string }
export interface AgendaUnit { id: string; condominiumId: string; identifier: string; blockId: string | null; block: string | null }
export interface AgendaRequestOption { id: string; protocol: string; title: string; residentName: string; unitIdentifier: string | null; block: string | null; status: string; linkedReminderId: string | null }
export interface AgendaOptions { units: AgendaUnit[]; requests: AgendaRequestOption[] }
export interface AgendaInput { title: string; description: string | null; unitId: string | null; relatedThirdParty: string | null; startsAtUtc: string; recurrenceType: AgendaRecurrence; notifyByWhatsApp: boolean; notifyByEmail: boolean; requestIds: string[] }
