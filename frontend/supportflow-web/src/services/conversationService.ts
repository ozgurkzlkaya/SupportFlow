import { apiClient } from '../api/apiClient'
import type {
  ConversationDetail,
  ConversationFilter,
  ConversationRequesterType,
  ConversationSummaryListResponse,
} from '../types/conversation'

export async function getConversations(
  search?: string,
  filter: ConversationFilter = 'All',
): Promise<ConversationSummaryListResponse> {
  const { data } = await apiClient.get<ConversationSummaryListResponse>('/api/conversations', {
    params: {
      ...(search?.trim() ? { search: search.trim() } : {}),
      ...(filter !== 'All' ? { filter } : {}),
    },
  })
  return data
}

export async function markConversationRead(
  requesterType: ConversationRequesterType,
  requesterId: number,
): Promise<void> {
  await apiClient.post(`/api/conversations/${requesterType}/${requesterId}/read`)
}

export async function pinConversation(
  requesterType: ConversationRequesterType,
  requesterId: number,
): Promise<void> {
  await apiClient.put(`/api/conversations/${requesterType}/${requesterId}/pin`)
}

export async function unpinConversation(
  requesterType: ConversationRequesterType,
  requesterId: number,
): Promise<void> {
  await apiClient.delete(`/api/conversations/${requesterType}/${requesterId}/pin`)
}

export async function getConversation(
  requesterType: ConversationRequesterType,
  requesterId: number,
): Promise<ConversationDetail> {
  const { data } = await apiClient.get<ConversationDetail>(
    `/api/conversations/${requesterType}/${requesterId}`,
  )
  return data
}
