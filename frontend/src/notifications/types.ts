export type NotificationType =
  | 'RequestCreated'
  | 'RequestStatusChanged'
  | 'RequestPriorityChanged'
  | 'RequestMessageReceived'
  | 'ResidentRequestUpdated'
  | 'ManagementCompanyRequestCreated'
  | 'ManagementCompanyRequestInfoRequested'
  | 'ManagementCompanyRequestManagerReplied'
  | 'ManagementCompanyRequestCompleted'
  | 'ManagementCompanyRequestCancelled'

export interface AppNotification {
  id: string
  condominiumId: string
  type: NotificationType | string
  title: string
  body: string
  requestId: string | null
  managementCompanyRequestId: string | null
  createdAt: string
  /** Null while unread. */
  readAt: string | null
}

export interface NotificationList {
  items: AppNotification[]
  unreadCount: number
}
