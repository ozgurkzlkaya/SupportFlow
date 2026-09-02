export type NotificationType =
  | 'TicketCreated'
  | 'CustomerReply'
  | 'TechnicalReply'
  | 'Assignment'
  | 'StatusChange'
  | 'GmailInbound'
  | 'GmailOutboundSuccess'
  | 'GmailOutboundFailure';

export interface SupportNotification {
  id: number;
  ticketId: number | null;
  title: string;
  message: string;
  type: NotificationType;
  isRead: boolean;
  createdAt: string;
}

export interface UnreadNotificationCount {
  count: number;
}

export interface MarkAllNotificationsReadResponse {
  updatedCount: number;
}
