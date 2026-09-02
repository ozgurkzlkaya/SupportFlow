import { apiClient } from '../api/apiClient'
import type {
  AppNotification,
  MarkAllNotificationsReadResponse,
  UnreadNotificationCountResponse,
} from '../types/notification'

export async function getNotifications(unreadOnly = false): Promise<AppNotification[]> {
  const { data } = await apiClient.get<AppNotification[]>('/api/notifications', {
    params: unreadOnly ? { unreadOnly: true } : undefined,
  })
  return data
}

export async function getUnreadNotificationCount(): Promise<number> {
  const { data } = await apiClient.get<UnreadNotificationCountResponse>(
    '/api/notifications/unread-count',
  )
  return data.count
}

export async function markNotificationAsRead(notificationId: number): Promise<void> {
  await apiClient.patch(`/api/notifications/${notificationId}/read`)
}

export async function markAllNotificationsAsRead(): Promise<number> {
  const { data } = await apiClient.patch<MarkAllNotificationsReadResponse>(
    '/api/notifications/read-all',
  )
  return data.updatedCount
}
