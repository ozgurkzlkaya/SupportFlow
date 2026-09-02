import type { QueueAgingBucket, SlaState, TicketPriority } from './ticket'
import type { TicketStatus } from './ticket'

export interface OldestWaitingTicket {
  ticketId: number
  ticketNumber: string
  title: string
  priority: TicketPriority
  assignedUserName: string | null
  awaitingStaffSince: string
  awaitingStaffMinutes: number
  agingBucket: QueueAgingBucket
  slaMostUrgentState: SlaState
}

export interface QueueAgingDashboard {
  awaitingReplyCount: number
  waitingOver2HoursCount: number
  waitingOver8HoursCount: number
  waitingOver24HoursCount: number
  oldestWaitingTickets: OldestWaitingTicket[]
}

export interface AgentWorkloadTotals {
  activeTicketCount: number
  unassignedTicketCount: number
  inactiveAssigneeTicketCount: number
  invalidAssigneeTicketCount: number
  criticalTicketCount: number
  highPriorityTicketCount: number
  awaitingReplyCount: number
  waitingOver2HoursCount: number
  waitingOver8HoursCount: number
  waitingOver24HoursCount: number
  slaBreachedCount: number
  slaDueSoonCount: number
}

export interface AgentWorkloadAgent {
  userId: number
  fullName: string
  email: string
  activeTicketCount: number
  criticalTicketCount: number
  highPriorityTicketCount: number
  awaitingReplyCount: number
  waitingOver8HoursCount: number
  waitingOver24HoursCount: number
  longestAwaitingMinutes: number | null
  slaBreachedCount: number
  slaDueSoonCount: number
}

export interface AgentWorkloadDashboard {
  generatedAt: string
  totals: AgentWorkloadTotals
  agents: AgentWorkloadAgent[]
}

export interface TicketDashboardRecentItem {
  id: number
  ticketNumber: string
  title: string
  status: TicketStatus
  priority: TicketPriority
  createdAt: string
  updatedAt: string
  customerName: string
}

export interface TicketDashboardSummary {
  totalCount: number
  openCount: number
  newCount: number
  inProgressCount: number
  waitingForCustomerCount: number
  reopenedCount: number
  resolvedOrClosedCount: number
  lowPriorityCount: number
  normalPriorityCount: number
  highPriorityCount: number
  criticalPriorityCount: number
  slaBreachedCount: number
  slaDueSoonCount: number
  firstResponseBreachedCount: number
  resolutionBreachedCount: number
  recentTickets: TicketDashboardRecentItem[]
}
