export type TicketStatus =
  | 'New'
  | 'Assigned'
  | 'InProgress'
  | 'WaitingForCustomer'
  | 'Resolved'
  | 'Closed'
  | 'Reopened';

export type TicketPriority = 'Low' | 'Normal' | 'High' | 'Critical';

export type TicketListSort =
  | 'NewestTicket'
  | 'OldestTicket'
  | 'UpdatedNewest'
  | 'UpdatedOldest'
  | 'LongestWaiting'
  | 'ShortestWaiting';

export type TicketAssignmentFilter = 'Unassigned' | 'InactiveAssignee';

export interface PaginationMetadata {
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

export interface PagedResponse<T> {
  items: T[];
  pagination: PaginationMetadata;
}

export interface ClassificationItem {
  id: number;
  name: string;
  isActive: boolean;
}

export interface TicketListItem {
  id: number;
  ticketNumber: string;
  title: string;
  status: TicketStatus;
  priority: TicketPriority;
  createdAt: string;
  updatedAt: string;
  customerName: string;
  requesterEmail: string;
  requesterType: 'RegisteredCustomer' | 'ExternalRequester';
  assignedUserId: number | null;
  assignedUserName: string | null;
  category: ClassificationItem | null;
}

export interface TicketDetail extends TicketListItem {
  description: string;
  closedAt: string | null;
  customerEmail: string;
  sla: {
    isTracked?: boolean;
    mostUrgentState?: string;
  };
}

export interface TicketMessage {
  id: number;
  ticketId: number;
  senderId: number | null;
  senderName: string;
  senderRole: 'Customer' | 'TechnicalStaff' | 'Admin';
  message: string;
  isInternal: boolean;
  deliveryStatus: 'NotApplicable' | 'Pending' | 'Sent' | 'Failed';
  deliveryAttemptCount: number;
  deliveredAt: string | null;
  lastDeliveryAttemptAt: string | null;
  safeDeliveryError: string | null;
  createdAt: string;
}

export interface TicketAttachment {
  id: number;
  ticketId: number;
  messageId: number | null;
  fileName: string;
  contentType: string;
  fileSize: number;
  isImage: boolean;
  createdAt: string;
  uploadedByUserName: string;
}

export interface TimelineItem {
  id: string;
  kind: string;
  occurredAt: string;
  title: string;
  summary: string | null;
  actor: { displayName: string; role: 'Customer' | 'TechnicalStaff' | 'Admin' | null };
  attachment: { fileName: string; contentType: string; fileSize: number } | null;
}

export interface TicketFilters {
  search: string;
  status?: TicketStatus;
  priority?: TicketPriority;
  sort?: TicketListSort;
  assignedUserId?: number;
  assignmentState?: TicketAssignmentFilter;
  activeOnly?: boolean;
}
