import type { TicketPriority, TicketStatus } from '@/types/tickets';

export type ConversationRequesterType = 'Registered' | 'External';
export type ConversationFilter = 'All' | 'AwaitingReply' | 'Unread' | 'Pinned';

export interface ConversationFilterCounts {
  all: number;
  awaitingReply: number;
  unread: number;
  pinned: number;
}

export interface ConversationSummary {
  requesterType: ConversationRequesterType;
  requesterId: number;
  displayName: string;
  email: string;
  ticketCount: number;
  openTicketCount: number;
  lastActivityAt: string;
  lastMessagePreview: string;
  latestTicketId: number;
  latestTicketNumber: string;
  latestTicketTitle: string;
  isPinned: boolean;
  isAwaitingStaffReply: boolean;
  awaitingReplyCount: number;
  isUnread: boolean;
  oldestAwaitingSince: string | null;
  oldestAwaitingMinutes: number | null;
}

export interface ConversationSummaryListResponse {
  items: ConversationSummary[];
  counts: ConversationFilterCounts;
}

export interface ConversationMessage {
  id: number;
  senderName: string;
  senderLabel: string;
  isSupportReply: boolean;
  message: string;
  deliveryStatus: 'NotApplicable' | 'Pending' | 'Sent' | 'Failed';
  createdAt: string;
}

export interface ConversationTicket {
  id: number;
  ticketNumber: string;
  title: string;
  status: TicketStatus;
  priority: TicketPriority;
  createdAt: string;
  lastActivityAt: string;
  initialMessage: string | null;
  messages: ConversationMessage[];
  attachments: { id: number; fileName: string; contentType: string; fileSize: number }[];
  isAwaitingStaffReply: boolean;
}

export interface ConversationDetail {
  requesterType: ConversationRequesterType;
  requesterId: number;
  displayName: string;
  email: string;
  ticketCount: number;
  openTicketCount: number;
  lastActivityAt: string;
  isPinned: boolean;
  isUnread: boolean;
  tickets: ConversationTicket[];
}
