export const NotificationType = {
  TicketCreated: 'TicketCreated',
  CustomerReply: 'CustomerReply',
  TechnicalReply: 'TechnicalReply',
  Assignment: 'Assignment',
  StatusChange: 'StatusChange',
  GmailInbound: 'GmailInbound',
  GmailOutboundSuccess: 'GmailOutboundSuccess',
  GmailOutboundFailure: 'GmailOutboundFailure',
} as const

export type NotificationType = (typeof NotificationType)[keyof typeof NotificationType]

export interface AppNotification {
  id: number
  ticketId: number | null
  title: string
  message: string
  type: NotificationType
  isRead: boolean
  createdAt: string
}

export interface UnreadNotificationCountResponse {
  count: number
}

export interface MarkAllNotificationsReadResponse {
  updatedCount: number
}
