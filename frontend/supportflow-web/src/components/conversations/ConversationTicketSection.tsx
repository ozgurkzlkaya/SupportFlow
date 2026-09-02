import { AttachmentOutlined, OpenInNewOutlined } from '@mui/icons-material'
import { Box, Button, Chip, Divider, Stack, Typography } from '@mui/material'
import type { ConversationTicket } from '../../types/conversation'
import { TicketPriorityChip } from '../common/TicketPriorityChip'
import { QueueAgingChip } from '../common/QueueAgingChip'
import { SlaStateChip } from '../common/SlaStateChip'
import { TicketStatusChip } from '../common/TicketStatusChip'
import { ConversationMessage } from './ConversationMessage'
import { PublicReplyComposer } from './PublicReplyComposer'
import { formatDuration } from '../../utils/formatDuration'

interface ConversationTicketSectionProps {
  ticket: ConversationTicket
  requesterName: string
  onOpenTicket: (ticketId: number) => void
  isReplyTarget: boolean
  onSelectReplyTarget: (ticketId: number) => void
  onMessagePersisted: () => Promise<void>
  onReplySucceeded: () => void
  onReplyDeliveryFailed: () => void
  onRetryDelivery: (ticketId: number, messageId: number) => void
  retryingMessageId: number | null
}

export function ConversationTicketSection({
  ticket,
  requesterName,
  onOpenTicket,
  isReplyTarget,
  onSelectReplyTarget,
  onMessagePersisted,
  onReplySucceeded,
  onReplyDeliveryFailed,
  onRetryDelivery,
  retryingMessageId,
}: ConversationTicketSectionProps) {
  return (
    <Box
      sx={{
        border: '1px solid',
        borderColor: isReplyTarget ? 'primary.main' : 'divider',
        borderRadius: 3,
        boxShadow: isReplyTarget ? '0 0 0 3px rgba(124, 58, 237, 0.08)' : 'none',
        overflow: 'hidden',
        transition: 'border-color 160ms ease, box-shadow 160ms ease',
      }}
    >
      <Stack
        direction={{ xs: 'column', sm: 'row' }}
        spacing={1.25}
        sx={{ alignItems: { sm: 'center' }, bgcolor: 'rgba(124, 58, 237, 0.045)', justifyContent: 'space-between', p: 1.5 }}
      >
        <Box>
          <Typography sx={{ fontWeight: 800 }} variant="body2">
            {ticket.ticketNumber}
          </Typography>
          <Typography sx={{ fontWeight: 700, mt: 0.25 }} variant="body1">
            {ticket.title}
          </Typography>
        </Box>
        <Stack direction="row" spacing={0.65} sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
          <TicketStatusChip status={ticket.status} />
          <TicketPriorityChip priority={ticket.priority} />
          <SlaStateChip state={ticket.sla.mostUrgentState} />
          <Button
            onClick={() => onSelectReplyTarget(ticket.id)}
            size="small"
            variant="contained"
          >
            {isReplyTarget ? 'Yanıtlanıyor' : 'Müşteriye Yanıtla'}
          </Button>
          <Button endIcon={<OpenInNewOutlined />} onClick={() => onOpenTicket(ticket.id)} size="small" variant="outlined">
            Talebi Aç
          </Button>
        </Stack>
      </Stack>

      <Stack spacing={1.25} sx={{ p: { xs: 1.25, sm: 1.75 } }}>
        {ticket.isAwaitingStaffReply && ticket.awaitingStaffMinutes !== null ? (
          <Stack direction="row" spacing={0.75} sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
            <Typography color="text.secondary" variant="caption">Bekleme</Typography>
            <Typography sx={{ fontWeight: 800 }} variant="body2">
              {formatDuration(ticket.awaitingStaffMinutes)}
            </Typography>
            {ticket.agingBucket ? <QueueAgingChip bucket={ticket.agingBucket} /> : null}
          </Stack>
        ) : null}
        {ticket.initialMessage ? (
          <Box sx={{ bgcolor: 'rgba(16, 185, 129, 0.07)', borderRadius: 3, p: 1.5 }}>
            <Typography color="success.dark" sx={{ fontWeight: 700 }} variant="caption">
              İlk Talep • {requesterName}
            </Typography>
            <Typography sx={{ mt: 0.55, whiteSpace: 'pre-wrap' }} variant="body2">
              {ticket.initialMessage}
            </Typography>
          </Box>
        ) : null}

        {ticket.messages.map((message) => (
          <ConversationMessage
            key={message.id}
            message={message}
            onRetryDelivery={(messageId) => onRetryDelivery(ticket.id, messageId)}
            retryingMessageId={retryingMessageId}
          />
        ))}

        {ticket.attachments.length > 0 ? (
          <>
            <Divider />
            <Stack direction="row" spacing={0.75} sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
              <AttachmentOutlined color="action" fontSize="small" />
              <Typography color="text.secondary" variant="caption">
                Ek dosyalar:
              </Typography>
              {ticket.attachments.map((attachment) => (
                <Chip key={attachment.id} label={attachment.fileName} size="small" variant="outlined" />
              ))}
            </Stack>
          </>
        ) : null}

        {isReplyTarget ? (
          <PublicReplyComposer
            onMessagePersisted={onMessagePersisted}
            onReplyDeliveryFailed={onReplyDeliveryFailed}
            onReplySucceeded={onReplySucceeded}
            ticket={ticket}
          />
        ) : null}
      </Stack>
    </Box>
  )
}
