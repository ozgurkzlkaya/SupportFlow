import { apiDownloadToFile, apiRequest } from '@/services/apiClient';
import type {
  PagedResponse,
  TicketAttachment,
  TicketDetail,
  TicketFilters,
  TicketListItem,
  TicketMessage,
  TicketPriority,
  TicketStatus,
  TimelineItem,
} from '@/types/tickets';

const TICKETS_PATH = '/api/tickets';

function buildQuery(values: Record<string, string | number | boolean | undefined>) {
  const params = new URLSearchParams();
  Object.entries(values).forEach(([key, value]) => {
    if (value !== undefined && value !== '') params.set(key, String(value));
  });
  const query = params.toString();
  return query ? `?${query}` : '';
}

export const ticketService = {
  getTickets(token: string, filters: TicketFilters, page: number) {
    return apiRequest<PagedResponse<TicketListItem>>(
      `${TICKETS_PATH}${buildQuery({
        page,
        pageSize: 25,
        search: filters.search.trim() || undefined,
        status: filters.status,
        priority: filters.priority,
        assignedUserId: filters.assignedUserId,
        assignmentState: filters.assignmentState,
        activeOnly: filters.activeOnly || undefined,
        sort: filters.sort,
      })}`,
      { token },
    );
  },

  getTicket(token: string, id: string) {
    return apiRequest<TicketDetail>(`${TICKETS_PATH}/${id}`, { token });
  },

  createTicket(token: string, request: { title: string; description: string; priority: TicketPriority }) {
    return apiRequest<TicketDetail>(TICKETS_PATH, { method: 'POST', token, body: request });
  },

  getMessages(token: string, id: string) {
    return apiRequest<TicketMessage[]>(`${TICKETS_PATH}/${id}/messages`, { token });
  },

  addMessage(token: string, id: string, message: string, isInternal = false) {
    return apiRequest<TicketMessage>(`${TICKETS_PATH}/${id}/messages`, {
      method: 'POST',
      token,
      body: { message, isInternal },
    });
  },

  assignTicket(token: string, id: string, assignedUserId: number) {
    return apiRequest<TicketDetail>(`${TICKETS_PATH}/${id}/assign`, {
      method: 'PATCH', token, body: { assignedUserId },
    });
  },

  updateStatus(token: string, id: string, status: TicketStatus, note?: string) {
    return apiRequest<TicketDetail>(`${TICKETS_PATH}/${id}/status`, {
      method: 'PATCH', token, body: { status, note: note?.trim() || null },
    });
  },

  updatePriority(token: string, id: string, priority: TicketPriority) {
    return apiRequest<TicketDetail>(`${TICKETS_PATH}/${id}/priority`, {
      method: 'PATCH', token, body: { priority },
    });
  },

  getAttachments(token: string, id: string) {
    return apiRequest<TicketAttachment[]>(`${TICKETS_PATH}/${id}/attachments`, { token });
  },

  uploadAttachment(token: string, id: string, file: { uri: string; name: string; mimeType?: string }) {
    const body = new FormData();
    body.append('file', {
      uri: file.uri,
      name: file.name,
      type: file.mimeType ?? 'application/octet-stream',
    } as unknown as Blob);
    return apiRequest<TicketAttachment>(`${TICKETS_PATH}/${id}/attachments`, { method: 'POST', token, body });
  },

  downloadAttachment(token: string, ticketId: string, attachment: TicketAttachment) {
    return apiDownloadToFile(
      `${TICKETS_PATH}/${ticketId}/attachments/${attachment.id}`,
      token,
      createDownloadFileName(attachment),
    );
  },

  getTimeline(token: string, id: string, page = 1) {
    return apiRequest<PagedResponse<TimelineItem>>(
      `${TICKETS_PATH}/${id}/timeline${buildQuery({ page, pageSize: 25 })}`,
      { token },
    );
  },
};

function createDownloadFileName(attachment: TicketAttachment) {
  const safeName = attachment.fileName
    .replace(/[^a-zA-Z0-9._-]+/g, '_')
    .replace(/^\.+/, '')
    .slice(0, 120)
    .trim() || `attachment-${attachment.id}`;
  const extension = /\.[a-z0-9]+$/i.test(safeName)
    ? ''
    : extensionByContentType[attachment.contentType.toLowerCase()] ?? '';
  const uniqueSuffix = `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`;

  // A fresh cache target prevents Android downloads from sharing a partially written file.
  return `supportflow-${attachment.id}-${uniqueSuffix}-${safeName}${extension}`;
}

const extensionByContentType: Record<string, string> = {
  'image/jpeg': '.jpg',
  'image/png': '.png',
  'image/webp': '.webp',
  'application/pdf': '.pdf',
};
