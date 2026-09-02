import { TicketStatus, type TicketStatus as TicketStatusType } from '../types/ticket'

const allowedTransitions: Record<TicketStatusType, TicketStatusType[]> = {
  [TicketStatus.New]: [
    TicketStatus.Assigned,
    TicketStatus.InProgress,
    TicketStatus.WaitingForCustomer,
    TicketStatus.Resolved,
    TicketStatus.Closed,
  ],
  [TicketStatus.Assigned]: [
    TicketStatus.InProgress,
    TicketStatus.WaitingForCustomer,
    TicketStatus.Resolved,
    TicketStatus.Closed,
  ],
  [TicketStatus.InProgress]: [
    TicketStatus.WaitingForCustomer,
    TicketStatus.Resolved,
    TicketStatus.Closed,
  ],
  [TicketStatus.WaitingForCustomer]: [
    TicketStatus.InProgress,
    TicketStatus.Resolved,
    TicketStatus.Closed,
  ],
  [TicketStatus.Resolved]: [
    TicketStatus.Closed,
    TicketStatus.InProgress,
    TicketStatus.Reopened,
  ],
  [TicketStatus.Closed]: [TicketStatus.Reopened],
  [TicketStatus.Reopened]: [
    TicketStatus.Assigned,
    TicketStatus.InProgress,
    TicketStatus.WaitingForCustomer,
    TicketStatus.Resolved,
    TicketStatus.Closed,
  ],
}

export function getAllowedNextStatuses(
  currentStatus: TicketStatusType,
): TicketStatusType[] {
  return allowedTransitions[currentStatus]
}
