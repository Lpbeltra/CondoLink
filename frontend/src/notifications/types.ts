export type NotificationType =
  | 'RequestCreated'
  | 'RequestStatusChanged'
  | 'RequestPriorityChanged'
  | 'RequestMessageReceived'
  | 'ResidentRequestUpdated'

export interface AppNotification {
  id: string
  condominiumId: string
  type: NotificationType | string
  title: string
  body: string
  requestId: string | null
  createdAt: string
  /** Null while unread. */
  readAt: string | null
}

export interface NotificationList {
  items: AppNotification[]
  unreadCount: number
}
