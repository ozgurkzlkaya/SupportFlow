import { apiClient } from '../api/apiClient'
import type { TicketAttachment } from '../types/attachment'

export async function getTicketAttachments(ticketId: number): Promise<TicketAttachment[]> {
  const { data } = await apiClient.get<TicketAttachment[]>(`/api/tickets/${ticketId}/attachments`)
  return data
}

export async function uploadTicketAttachment(
  ticketId: number,
  file: File,
): Promise<TicketAttachment> {
  const formData = new FormData()
  formData.append('file', file)

  const { data } = await apiClient.post<TicketAttachment>(
    `/api/tickets/${ticketId}/attachments`,
    formData,
  )

  return data
}

export async function downloadTicketAttachment(
  ticketId: number,
  attachmentId: number,
): Promise<Blob> {
  const { data } = await apiClient.get(
    `/api/tickets/${ticketId}/attachments/${attachmentId}`,
    {
      responseType: 'blob',
    },
  )

  return data as Blob
}
