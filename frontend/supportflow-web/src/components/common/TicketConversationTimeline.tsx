import { LockOutlined, PersonOutlined, SupportAgentOutlined } from '@mui/icons-material'
import { Avatar, Box, Chip, Stack, Typography } from '@mui/material'
import { UserRole } from '../../types/auth'
import { type TicketMessage } from '../../types/ticket'
import { OutboundDeliveryState } from './OutboundDeliveryState'
import { formatDate } from '../../utils/formatDate'

type BaseConversationItem = {
  id: string | number
  senderName: string
  roleLabel?: string
  message: string
  createdAt: string
  ticketId?: number
  deliveryStatus?: TicketMessage['deliveryStatus']
  deliveryAttemptCount?: number
  safeDeliveryError?: string | null
  tone: 'customer' | 'support' | 'internal' | 'initial'
  markerLabel?: string
}

interface TicketConversationTimelineProps {
  initialMessage?: {
    senderName: string
    message: string
    createdAt: string
    markerLabel?: string
  }
  messages: TicketMessage[]
  emptyText: string
  onRetryDelivery?: (ticketId: number, messageId: number) => void
  retryingMessageId?: number | null
}

function getToneStyles(tone: BaseConversationItem['tone']) {
  if (tone === 'customer') {
    return {
      accent: '#334155',
      avatarBg: 'rgba(148, 163, 184, 0.18)',
      avatarIcon: <PersonOutlined fontSize="small" />,
      chipColor: 'default' as const,
      surface: '#f8fafc',
    }
  }

  if (tone === 'support') {
    return {
      accent: '#2563eb',
      avatarBg: 'rgba(37, 99, 235, 0.12)',
      avatarIcon: <SupportAgentOutlined fontSize="small" />,
      chipColor: 'primary' as const,
      surface: 'rgba(239, 246, 255, 0.88)',
    }
  }

  if (tone === 'internal') {
    return {
      accent: '#b45309',
      avatarBg: 'rgba(245, 158, 11, 0.16)',
      avatarIcon: <LockOutlined fontSize="small" />,
      chipColor: 'warning' as const,
      surface: 'rgba(255, 251, 235, 0.94)',
    }
  }

  return {
    accent: '#0f766e',
    avatarBg: 'rgba(15, 118, 110, 0.12)',
    avatarIcon: <SupportAgentOutlined fontSize="small" />,
    chipColor: 'success' as const,
    surface: 'rgba(240, 253, 250, 0.92)',
  }
}

export function TicketConversationTimeline({
  initialMessage,
  messages,
  emptyText,
  onRetryDelivery,
  retryingMessageId,
}: TicketConversationTimelineProps) {
  const items: BaseConversationItem[] = []

  if (initialMessage) {
    items.push({
      id: 'initial',
      senderName: initialMessage.senderName,
      message: initialMessage.message,
      createdAt: initialMessage.createdAt,
      markerLabel: initialMessage.markerLabel ?? 'İlk Talep',
      tone: 'initial',
    })
  }

  items.push(
    ...messages.map(
      (message): BaseConversationItem => ({
        id: message.id,
        senderName: message.senderName,
        roleLabel: message.isInternal
          ? 'İç Not'
          : message.senderRole === UserRole.Customer
            ? 'Müşteri'
            : 'Destek',
        message: message.message,
        createdAt: message.createdAt,
        ticketId: message.ticketId,
        deliveryStatus: message.deliveryStatus,
        deliveryAttemptCount: message.deliveryAttemptCount,
        safeDeliveryError: message.safeDeliveryError,
        tone: message.isInternal
          ? 'internal'
          : message.senderRole === UserRole.Customer
            ? 'customer'
            : 'support',
      }),
    ),
  )

  if (items.length === 0) {
    return (
      <Typography color="text.secondary" sx={{ py: 1, textAlign: 'center' }}>
        {emptyText}
      </Typography>
    )
  }

  return (
    <Stack spacing={1.5}>
      {items.map((item) => {
        const toneStyles = getToneStyles(item.tone)
        const failedDelivery = item.tone === 'support' && item.deliveryStatus === 'Failed'
        const retrying = item.id === retryingMessageId

        return (
          <Box
            key={item.id}
            sx={{
              border: '1px solid',
              borderColor: failedDelivery ? 'error.light' : 'divider',
              borderRadius: 3,
              maxWidth: '100%',
              overflow: 'hidden',
            }}
          >
            <Stack
              direction="row"
              spacing={1.25}
              sx={{
                alignItems: 'flex-start',
                bgcolor: toneStyles.surface,
                borderBottom: '1px solid',
                borderBottomColor: 'divider',
                px: 1.75,
                py: 1.4,
              }}
            >
              <Avatar
                sx={{
                  bgcolor: toneStyles.avatarBg,
                  color: toneStyles.accent,
                  height: 34,
                  width: 34,
                }}
              >
                {toneStyles.avatarIcon}
              </Avatar>

              <Box sx={{ flexGrow: 1, minWidth: 0 }}>
                <Stack
                  direction={{ xs: 'column', sm: 'row' }}
                  spacing={0.8}
                  sx={{ alignItems: { sm: 'center' }, justifyContent: 'space-between' }}
                >
                  <Stack direction="row" spacing={0.8} sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
                    <Typography sx={{ fontWeight: 700 }} variant="body2">
                      {item.senderName}
                    </Typography>
                    {(item.markerLabel || item.roleLabel) && (
                      <Chip
                        color={toneStyles.chipColor}
                        label={item.markerLabel ?? item.roleLabel}
                        size="small"
                        variant={item.tone === 'customer' ? 'outlined' : 'filled'}
                      />
                    )}
                  </Stack>
                  <Typography color="text.secondary" variant="caption">
                    {formatDate(item.createdAt)}
                  </Typography>
                </Stack>
              </Box>
            </Stack>

            <Stack spacing={1.25} sx={{ p: 1.75 }}>
              <Typography sx={{ maxWidth: '82ch', whiteSpace: 'pre-wrap' }} variant="body2">
                {item.message}
              </Typography>
              {item.tone === 'support' ? (
                <OutboundDeliveryState
                  attemptCount={item.deliveryAttemptCount}
                  deliveryStatus={item.deliveryStatus}
                  onRetry={
                    onRetryDelivery && item.ticketId
                      ? () => onRetryDelivery(item.ticketId!, Number(item.id))
                      : undefined
                  }
                  retrying={retrying}
                  safeDeliveryError={item.safeDeliveryError}
                />
              ) : null}
            </Stack>
          </Box>
        )
      })}
    </Stack>
  )
}
