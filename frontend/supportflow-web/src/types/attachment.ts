export interface TicketAttachment {
  id: number
  ticketId: number
  messageId: number | null
  fileName: string
  contentType: string
  fileSize: number
  isImage: boolean
  createdAt: string
  uploadedByUserId: number | null
  uploadedByUserName: string
  uploadedByExternalRequesterId: number | null
  uploadedByExternalRequesterName: string
}
