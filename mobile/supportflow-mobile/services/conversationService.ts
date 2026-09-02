import { apiRequest } from '@/services/apiClient';
import type {
  ConversationDetail,
  ConversationFilter,
  ConversationRequesterType,
  ConversationSummaryListResponse,
} from '@/types/conversations';

const CONVERSATIONS_PATH = '/api/conversations';

export const conversationService = {
  getConversations(token: string, search = '', filter: ConversationFilter = 'All') {
    const params = new URLSearchParams();
    if (search.trim()) params.set('search', search.trim());
    if (filter !== 'All') params.set('filter', filter);
    const query = params.toString();
    return apiRequest<ConversationSummaryListResponse>(`${CONVERSATIONS_PATH}${query ? `?${query}` : ''}`, { token });
  },

  getConversation(token: string, requesterType: ConversationRequesterType, requesterId: number) {
    return apiRequest<ConversationDetail>(`${CONVERSATIONS_PATH}/${requesterType}/${requesterId}`, { token });
  },

  markRead(token: string, requesterType: ConversationRequesterType, requesterId: number) {
    return apiRequest<void>(`${CONVERSATIONS_PATH}/${requesterType}/${requesterId}/read`, { method: 'POST', token });
  },

  setPinned(token: string, requesterType: ConversationRequesterType, requesterId: number, pinned: boolean) {
    return apiRequest<void>(`${CONVERSATIONS_PATH}/${requesterType}/${requesterId}/pin`, {
      method: pinned ? 'PUT' : 'DELETE',
      token,
    });
  },
};
