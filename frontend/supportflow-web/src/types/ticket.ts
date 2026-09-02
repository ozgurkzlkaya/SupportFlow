import type { UserRole } from './auth'
import type { TicketClassificationItem } from './taxonomy'

export const TicketStatus = {
  New: 'New',
  Assigned: 'Assigned',
  InProgress: 'InProgress',
  WaitingForCustomer: 'WaitingForCustomer',
  Resolved: 'Resolved',
  Closed: 'Closed',
  Reopened: 'Reopened',
} as const

export type TicketStatus = (typeof TicketStatus)[keyof typeof TicketStatus]

export const TicketPriority = {
  Low: 'Low',
  Normal: 'Normal',
  High: 'High',
  Critical: 'Critical',
} as const

export type TicketPriority = (typeof TicketPriority)[keyof typeof TicketPriority]

export const DeliveryStatus = {
  NotApplicable: 'NotApplicable',
  Pending: 'Pending',
  Sent: 'Sent',
  Failed: 'Failed',
} as const

export type DeliveryStatus = (typeof DeliveryStatus)[keyof typeof DeliveryStatus]

export const SlaState = {
  NotTracked: 'NotTracked',
  OnTrack: 'OnTrack',
  DueSoon: 'DueSoon',
  Breached: 'Breached',
  Met: 'Met',
} as const

export type SlaState = (typeof SlaState)[keyof typeof SlaState]

export const QueueAgingBucket = {
  New: 'New',
  ShortWait: 'ShortWait',
  Waiting: 'Waiting',
  LongWait: 'LongWait',
  Overdue: 'Overdue',
  CriticalWait: 'CriticalWait',
} as const

export type QueueAgingBucket = (typeof QueueAgingBucket)[keyof typeof QueueAgingBucket]

export const TicketAgingFilter = {
  Awaiting: 'Awaiting',
  WaitingOver2Hours: 'WaitingOver2Hours',
  WaitingOver8Hours: 'WaitingOver8Hours',
  WaitingOver24Hours: 'WaitingOver24Hours',
} as const

export type TicketAgingFilter = (typeof TicketAgingFilter)[keyof typeof TicketAgingFilter]

export const TicketAssignmentFilter = {
  Unassigned: 'Unassigned',
  InactiveAssignee: 'InactiveAssignee',
} as const

export type TicketAssignmentFilter =
  (typeof TicketAssignmentFilter)[keyof typeof TicketAssignmentFilter]

export const TicketListSort = {
  NewestTicket: 'NewestTicket',
  OldestTicket: 'OldestTicket',
  UpdatedNewest: 'UpdatedNewest',
  UpdatedOldest: 'UpdatedOldest',
  LongestWaiting: 'LongestWaiting',
  ShortestWaiting: 'ShortestWaiting',
} as const

export type TicketListSort = (typeof TicketListSort)[keyof typeof TicketListSort]

export interface TicketSlaMetric {
  state: SlaState
  dueAt: string | null
  completedAt: string | null
  remainingMinutes: number | null
  overdueMinutes: number | null
}

export interface TicketSlaSummary {
  firstResponseState: SlaState
  resolutionState: SlaState
  mostUrgentState: SlaState
}

export interface TicketSla {
  isTracked: boolean
  resolutionCycleStartedAt: string | null
  firstResponse: TicketSlaMetric
  resolution: TicketSlaMetric
  mostUrgentState: SlaState
}

export interface CreateTicketRequest {
  title: string
  description: string
  priority: TicketPriority
}

export interface TicketListItem {
  id: number
  ticketNumber: string
  title: string
  status: TicketStatus
  priority: TicketPriority
  createdAt: string
  updatedAt: string
  customerId: number | null
  externalRequesterId: number | null
  customerName: string
  requesterEmail: string
  requesterType: 'RegisteredCustomer' | 'ExternalRequester'
  assignedUserId: number | null
  assignedUserName: string | null
  category: TicketClassificationItem | null
  tags: TicketClassificationItem[]
  isAwaitingStaffReply: boolean
  awaitingStaffSince: string | null
  awaitingStaffMinutes: number | null
  ticketAgeMinutes: number
  agingBucket: QueueAgingBucket | null
  sla: TicketSlaSummary
}

export interface TicketDetail {
  id: number
  ticketNumber: string
  title: string
  description: string
  status: TicketStatus
  priority: TicketPriority
  createdAt: string
  updatedAt: string
  closedAt: string | null
  customerId: number | null
  externalRequesterId: number | null
  customerName: string
  customerEmail: string
  requesterType: 'RegisteredCustomer' | 'ExternalRequester'
  assignedUserId: number | null
  assignedUserName: string | null
  category: TicketClassificationItem | null
  tags: TicketClassificationItem[]
  isAwaitingStaffReply: boolean
  awaitingStaffSince: string | null
  awaitingStaffMinutes: number | null
  ticketAgeMinutes: number
  agingBucket: QueueAgingBucket | null
  sla: TicketSla
}

export interface CreateTicketMessageRequest {
  message: string
  isInternal: boolean
}

export interface TicketMessage {
  id: number
  ticketId: number
  senderId: number | null
  senderName: string
  senderRole: UserRole
  message: string
  isInternal: boolean
  deliveryStatus: DeliveryStatus
  deliveryAttemptCount: number
  deliveredAt: string | null
  lastDeliveryAttemptAt: string | null
  safeDeliveryError: string | null
  createdAt: string
}

export interface UpdateTicketStatusRequest {
  status: TicketStatus
  note: string | null
}

export interface TicketStatusHistory {
  id: number
  oldStatus: TicketStatus
  newStatus: TicketStatus
  changedByUserId: number
  changedByUserName: string
  note: string | null
  changedAt: string
}
