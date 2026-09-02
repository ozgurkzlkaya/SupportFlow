import { Alert, Stack, Typography } from '@mui/material'
import { useEffect, useState } from 'react'
import type { ConversationTicket } from '../../types/conversation'
import { ConversationTicketSection } from './ConversationTicketSection'

interface ConversationTimelineProps {
  tickets: ConversationTicket[]
  requesterName: string
  onOpenTicket: (ticketId: number) => void
  onMessagePersisted: () => Promise<void>
  onRetryDelivery: (ticketId: number, messageId: number) => void
  retryingMessageId: number | null
}

export function ConversationTimeline({
  tickets,
  requesterName,
  onOpenTicket,
  onMessagePersisted,
  onRetryDelivery,
  retryingMessageId,
}: ConversationTimelineProps) {
  const [replyTicketId, setReplyTicketId] = useState<number | null>(null)
  const [replySuccess, setReplySuccess] = useState('')
  const [replyWarning, setReplyWarning] = useState('')

  useEffect(() => {
    setReplyTicketId((currentTicketId) =>
      currentTicketId && tickets.some((ticket) => ticket.id === currentTicketId)
        ? currentTicketId
        : null,
    )
  }, [tickets])

  function selectReplyTarget(ticketId: number) {
    setReplySuccess('')
    setReplyWarning('')
    setReplyTicketId(ticketId)
  }

  function handleReplySucceeded() {
    setReplyTicketId(null)
    setReplyWarning('')
    setReplySuccess('Yanıt müşteriye başarıyla gönderildi.')
  }

  function handleReplyDeliveryFailed() {
    setReplyTicketId(null)
    setReplySuccess('')
    setReplyWarning('Yanıt talebe kaydedildi ancak e-posta gönderilemedi. Mesaj üzerinden yeniden deneyebilirsiniz.')
  }

  if (tickets.length === 0) {
    return (
      <Typography color="text.secondary" sx={{ py: 3, textAlign: 'center' }} variant="body2">
        Bu müşteri için görüntülenecek public iletişim bulunmuyor.
      </Typography>
    )
  }

  return (
    <Stack spacing={2}>
      {replySuccess ? <Alert severity="success">{replySuccess}</Alert> : null}
      {replyWarning ? <Alert severity="warning">{replyWarning}</Alert> : null}
      {tickets.map((ticket) => (
        <ConversationTicketSection
          key={ticket.id}
          isReplyTarget={replyTicketId === ticket.id}
          onMessagePersisted={onMessagePersisted}
          onOpenTicket={onOpenTicket}
          onReplyDeliveryFailed={handleReplyDeliveryFailed}
          onReplySucceeded={handleReplySucceeded}
          onRetryDelivery={onRetryDelivery}
          onSelectReplyTarget={selectReplyTarget}
          requesterName={requesterName}
          retryingMessageId={retryingMessageId}
          ticket={ticket}
        />
      ))}
    </Stack>
  )
}
