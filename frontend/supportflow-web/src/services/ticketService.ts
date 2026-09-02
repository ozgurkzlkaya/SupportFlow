import { apiClient } from '../api/apiClient'
import type {
  CreateTicketMessageRequest,
  CreateTicketRequest,
  TicketDetail,
  TicketListItem,
  TicketMessage,
  TicketPriority,
  TicketAgingFilter,
  TicketAssignmentFilter,
  TicketStatusHistory,
  TicketStatus,
  TicketListSort,
  UpdateTicketStatusRequest,
} from '../types/ticket'
import type { TicketClassification, UpdateTicketClassificationRequest } from '../types/taxonomy'
import type { TicketDashboardSummary } from '../types/dashboard'
import type { PagedResponse } from '../types/pagination'
import type { TicketTimelineItem } from '../types/timeline'

export interface TicketListQuery {
  priority?: TicketPriority
  status?: TicketStatus
  assignedUserId?: number
  assignmentState?: TicketAssignmentFilter
  activeOnly?: boolean
  search?: string
  agingFilter?: TicketAgingFilter
  sort?: TicketListSort
  categoryId?: number
  classificationState?: 'Unclassified'
  tagId?: number
  page?: number
  pageSize?: number
}

export async function getTickets(query?: TicketListQuery): Promise<PagedResponse<TicketListItem>> {
  const { data } = await apiClient.get<PagedResponse<TicketListItem>>('/api/tickets', {
    params: query,
  })
  return data
}

export async function getTicketDashboardSummary(): Promise<TicketDashboardSummary> {
  const { data } = await apiClient.get<TicketDashboardSummary>('/api/tickets/summary')
  return data
}

export async function getTicket(id: number): Promise<TicketDetail> {
  const { data } = await apiClient.get<TicketDetail>(`/api/tickets/${id}`)
  return data
}

export async function getTicketTimeline(
  ticketId: number,
  page = 1,
  pageSize = 25,
): Promise<PagedResponse<TicketTimelineItem>> {
  const { data } = await apiClient.get<PagedResponse<TicketTimelineItem>>(
    `/api/tickets/${ticketId}/timeline`,
    { params: { page, pageSize } },
  )
  return data
}

export async function createTicket(request: CreateTicketRequest): Promise<TicketDetail> {
  const { data } = await apiClient.post<TicketDetail>('/api/tickets', request)
  return data
}

export async function getMessages(ticketId: number): Promise<TicketMessage[]> {
  const { data } = await apiClient.get<TicketMessage[]>(
    `/api/tickets/${ticketId}/messages`,
  )
  return data
}

export async function sendMessage(
  ticketId: number,
  request: CreateTicketMessageRequest,
): Promise<TicketMessage> {
  const { data } = await apiClient.post<TicketMessage>(
    `/api/tickets/${ticketId}/messages`,
    request,
  )
  return data
}

export async function retryMessageDelivery(
  ticketId: number,
  messageId: number,
): Promise<TicketMessage> {
  const { data } = await apiClient.post<TicketMessage>(
    `/api/tickets/${ticketId}/messages/${messageId}/retry-delivery`,
  )
  return data
}

export async function assignTicket(
  ticketId: number,
  assignedUserId: number,
): Promise<TicketDetail> {
  const { data } = await apiClient.patch<TicketDetail>(
    `/api/tickets/${ticketId}/assign`,
    { assignedUserId },
  )
  return data
}

export async function updateStatus(
  ticketId: number,
  request: UpdateTicketStatusRequest,
): Promise<TicketDetail> {
  const { data } = await apiClient.patch<TicketDetail>(
    `/api/tickets/${ticketId}/status`,
    request,
  )
  return data
}

export async function updatePriority(
  ticketId: number,
  priority: TicketPriority,
): Promise<TicketDetail> {
  const { data } = await apiClient.patch<TicketDetail>(
    `/api/tickets/${ticketId}/priority`,
    { priority },
  )
  return data
}

export async function updateClassification(
  ticketId: number,
  request: UpdateTicketClassificationRequest,
): Promise<TicketClassification> {
  const { data } = await apiClient.patch<TicketClassification>(
    `/api/tickets/${ticketId}/classification`,
    request,
  )
  return data
}

export async function getHistory(ticketId: number): Promise<TicketStatusHistory[]> {
  const { data } = await apiClient.get<TicketStatusHistory[]>(
    `/api/tickets/${ticketId}/history`,
  )
  return data
}
