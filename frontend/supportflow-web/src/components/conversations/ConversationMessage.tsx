import { PersonOutlined, SupportAgentOutlined } from '@mui/icons-material'
import { Avatar, Box, Chip, Stack, Typography } from '@mui/material'
import { OutboundDeliveryState } from '../common/OutboundDeliveryState'
import type { ConversationMessage as ConversationMessageData } from '../../types/conversation'
import { formatDate } from '../../utils/formatDate'

interface ConversationMessageProps {
  message: ConversationMessageData
  onRetryDelivery?: (messageId: number) => void
  retryingMessageId?: number | null
}

export function ConversationMessage({
  message,
  onRetryDelivery,
  retryingMessageId,
}: ConversationMessageProps) {
  const support = message.isSupportReply
  const failedDelivery = support && message.deliveryStatus === 'Failed'
  const retrying = retryingMessageId === message.id

  return (
    <Stack sx={{ alignItems: support ? 'flex-end' : 'flex-start' }}>
      <Box
        sx={{
          bgcolor: failedDelivery
            ? 'rgba(220, 38, 38, 0.06)'
            : support ? 'rgba(109, 40, 217, 0.09)' : 'rgba(16, 185, 129, 0.09)',
          border: '1px solid',
          borderColor: failedDelivery
            ? 'rgba(220, 38, 38, 0.32)'
            : support ? 'rgba(109, 40, 217, 0.2)' : 'rgba(16, 185, 129, 0.2)',
          borderRadius: 3,
          maxWidth: 'min(100%, 760px)',
          px: 1.5,
          py: 1.25,
        }}
      >
        <Stack direction="row" spacing={1} sx={{ alignItems: 'center', mb: 0.8 }}>
          <Avatar
            sx={{
              bgcolor: support ? 'rgba(109, 40, 217, 0.15)' : 'rgba(16, 185, 129, 0.15)',
              color: support ? 'primary.main' : '#047857',
              height: 28,
              width: 28,
            }}
          >
            {support ? <SupportAgentOutlined fontSize="small" /> : <PersonOutlined fontSize="small" />}
          </Avatar>
          <Box sx={{ flexGrow: 1 }}>
            <Typography sx={{ fontWeight: 700 }} variant="body2">
              {message.senderName}
            </Typography>
            <Chip label={message.senderLabel} size="small" sx={{ height: 20, mt: 0.35 }} />
          </Box>
          <Typography color="text.secondary" variant="caption">
            {formatDate(message.createdAt)}
          </Typography>
        </Stack>
        <Stack spacing={1}>
          <Typography sx={{ whiteSpace: 'pre-wrap' }} variant="body2">
            {message.message}
          </Typography>
          {support ? (
            <OutboundDeliveryState
              attemptCount={message.deliveryAttemptCount}
              deliveryStatus={message.deliveryStatus}
              onRetry={onRetryDelivery ? () => onRetryDelivery(message.id) : undefined}
              retrying={retrying}
              safeDeliveryError={message.safeDeliveryError}
            />
          ) : null}
        </Stack>
      </Box>
    </Stack>
  )
}
