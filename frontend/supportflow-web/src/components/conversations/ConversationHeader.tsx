import { OpenInNewOutlined } from '@mui/icons-material'
import { Button, Stack, Typography } from '@mui/material'
import { RequesterTypeChip } from '../common/RequesterTypeChip'
import type { ConversationDetail } from '../../types/conversation'
import { formatDate } from '../../utils/formatDate'

interface ConversationHeaderProps {
  conversation: ConversationDetail
  onViewTickets: () => void
}

export function ConversationHeader({ conversation, onViewTickets }: ConversationHeaderProps) {
  return (
    <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} sx={{ justifyContent: 'space-between' }}>
      <Stack spacing={0.65} sx={{ minWidth: 0 }}>
        <Stack direction="row" spacing={1} sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
          <Typography sx={{ fontWeight: 800 }} variant="h5">
            {conversation.displayName}
          </Typography>
          <RequesterTypeChip requesterType={conversation.requesterType} />
        </Stack>
        <Typography color="text.secondary" variant="body2">
          {conversation.email}
        </Typography>
        <Stack direction="row" spacing={2} sx={{ color: 'text.secondary', flexWrap: 'wrap' }}>
          <Typography variant="caption">Toplam: {conversation.ticketCount} talep</Typography>
          <Typography variant="caption">Açık: {conversation.openTicketCount} talep</Typography>
          <Typography variant="caption">Son aktivite: {formatDate(conversation.lastActivityAt)}</Typography>
        </Stack>
      </Stack>
      <Button endIcon={<OpenInNewOutlined />} onClick={onViewTickets} variant="outlined">
        Talepleri Gör
      </Button>
    </Stack>
  )
}
