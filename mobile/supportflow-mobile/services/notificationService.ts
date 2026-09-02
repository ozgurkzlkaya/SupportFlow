import { apiRequest } from '@/services/apiClient';
import type { MarkAllNotificationsReadResponse, SupportNotification, UnreadNotificationCount } from '@/types/notifications';

const NOTIFICATIONS_PATH = '/api/notifications';

export const notificationService = {
  getNotifications(token: string) {
    return apiRequest<SupportNotification[]>(NOTIFICATIONS_PATH, { token });
  },
  getUnreadCount(token: string) {
    return apiRequest<UnreadNotificationCount>(`${NOTIFICATIONS_PATH}/unread-count`, { token });
  },
  markAsRead(token: string, id: number) {
    return apiRequest<void>(`${NOTIFICATIONS_PATH}/${id}/read`, { method: 'PATCH', token });
  },
  markAllAsRead(token: string) {
    return apiRequest<MarkAllNotificationsReadResponse>(`${NOTIFICATIONS_PATH}/read-all`, { method: 'PATCH', token });
  },
};
