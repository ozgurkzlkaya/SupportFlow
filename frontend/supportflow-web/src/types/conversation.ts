import type {
  DeliveryStatus,
  QueueAgingBucket,
  TicketPriority,
  TicketSlaSummary,
  TicketStatus,
} from './ticket'

export type ConversationRequesterType = 'Registered' | 'External'
export type ConversationFilter = 'All' | 'AwaitingReply' | 'Unread' | 'Pinned'

export interface ConversationFilterCounts {
  all: number
  awaitingReply: number
  unread: number
  pinned: number
}

export interface ConversationSummaryListResponse {
  items: ConversationSummary[]
  counts: ConversationFilterCounts
}

export interface ConversationSummary {
  requesterType: ConversationRequesterType
  requesterId: number
  displayName: string
  email: string
  ticketCount: number
  openTicketCount: number
  lastActivityAt: string
  lastMessagePreview: string
  latestTicketId: number
  latestTicketNumber: string
  latestTicketTitle: string
  isPinned: boolean
  isAwaitingStaffReply: boolean
  awaitingReplyCount: number
  isUnread: boolean
  lastRequesterActivityAt: string | null
  oldestAwaitingSince: string | null
  oldestAwaitingMinutes: number | null
  slaBreachedTicketCount: number
  slaDueSoonTicketCount: number
}

export interface ConversationAttachment {
  id: number
  fileName: string
  contentType: string
  fileSize: number
}

export interface ConversationMessage {
  id: number
  senderName: string
  senderLabel: string
  isSupportReply: boolean
  message: string
  deliveryStatus: DeliveryStatus
  deliveryAttemptCount: number
  deliveredAt: string | null
  lastDeliveryAttemptAt: string | null
  safeDeliveryError: string | null
  createdAt: string
}

export interface ConversationTicket {
  id: number
  ticketNumber: string
  title: string
  status: TicketStatus
  priority: TicketPriority
  createdAt: string
  lastActivityAt: string
  initialMessage: string | null
  messages: ConversationMessage[]
  attachments: ConversationAttachment[]
  isAwaitingStaffReply: boolean
  awaitingStaffSince: string | null
  awaitingStaffMinutes: number | null
  agingBucket: QueueAgingBucket | null
  sla: TicketSlaSummary
}

export interface ConversationDetail {
  requesterType: ConversationRequesterType
  requesterId: number
  displayName: string
  email: string
  ticketCount: number
  openTicketCount: number
  lastActivityAt: string
  isPinned: boolean
  isUnread: boolean
  oldestAwaitingSince: string | null
  oldestAwaitingMinutes: number | null
  tickets: ConversationTicket[]
}
