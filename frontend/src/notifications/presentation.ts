import type { AppNotification } from './types'

/** Cap shown on the badge; above this it becomes "9+". */
export const badgeCap = 9

export function formatBadgeCount(unreadCount: number): string {
  if (unreadCount <= 0) return ''
  return unreadCount > badgeCap ? `${badgeCap}+` : String(unreadCount)
}

/**
 * Short relative time in pt-BR.
 * Uses "agora" under a minute rather than "0 min", which reads as broken.
 */
export function formatRelativeTime(
  isoDate: string,
  now: number = Date.now(),
): string {
  const timestamp = Date.parse(isoDate)
  if (Number.isNaN(timestamp)) return ''

  const seconds = Math.floor((now - timestamp) / 1000)
  if (seconds < 0) return 'agora'
  if (seconds < 60) return 'agora'

  const minutes = Math.floor(seconds / 60)
  if (minutes < 60) return `${minutes} min`

  const hours = Math.floor(minutes / 60)
  if (hours < 24) return `${hours} h`

  const days = Math.floor(hours / 24)
  if (days < 7) return `${days} d`

  const weeks = Math.floor(days / 7)
  if (weeks < 5) return `${weeks} sem`

  const months = Math.floor(days / 30)
  return months < 12 ? `${months} mes${months === 1 ? '' : 'es'}` : `${Math.floor(days / 365)} a`
}

export function isUnread(notification: AppNotification): boolean {
  return notification.readAt === null
}

export function countUnread(notifications: AppNotification[]): number {
  return notifications.filter(isUnread).length
}

/**
 * Deep-link target for a notification, or null when it does not point anywhere.
 * Managers land on the management view of a request; residents on their own.
 */
export function notificationLink(
  notification: AppNotification,
  isManager: boolean,
): string | null {
  if (!notification.requestId) return null
  return isManager
    ? `/management/requests/${notification.requestId}`
    : `/requests/${notification.requestId}`
}

/**
 * Applies a local read mark without refetching, so the UI responds instantly.
 * Returns a new array; already-read items keep their original timestamp.
 */
export function markReadLocally(
  notifications: AppNotification[],
  id: string,
  readAt: string,
): AppNotification[] {
  return notifications.map((notification) =>
    notification.id === id && notification.readAt === null
      ? { ...notification, readAt }
      : notification,
  )
}

export function markAllReadLocally(
  notifications: AppNotification[],
  readAt: string,
): AppNotification[] {
  return notifications.map((notification) =>
    notification.readAt === null ? { ...notification, readAt } : notification,
  )
}
