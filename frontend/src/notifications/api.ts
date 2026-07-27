import { api } from '../services/api'
import type { NotificationList } from './types'

export async function listNotifications(params: {
  condominiumId?: string
  unreadOnly?: boolean
  take?: number
} = {}) {
  return (await api.get<NotificationList>('/notifications', { params })).data
}

export async function getUnreadCount() {
  return (
    await api.get<{ unreadCount: number }>('/notifications/unread-count')
  ).data.unreadCount
}

export async function markNotificationRead(id: string) {
  await api.patch(`/notifications/${id}/read`)
}

export async function markAllNotificationsRead(condominiumId?: string) {
  return (
    await api.patch<{ updated: number }>(
      '/notifications/read-all',
      null,
      { params: condominiumId ? { condominiumId } : undefined },
    )
  ).data.updated
}
