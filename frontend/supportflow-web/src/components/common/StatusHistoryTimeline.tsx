import { ArrowForward } from '@mui/icons-material'
import { Box, Stack, Typography } from '@mui/material'
import type { TicketStatusHistory } from '../../types/ticket'
import { formatDate } from '../../utils/formatDate'
import { getTicketStatusLabel } from '../../utils/ticketLabels'

interface StatusHistoryTimelineProps {
  history: TicketStatusHistory[]
  emptyText: string
}

export function StatusHistoryTimeline({
  history,
  emptyText,
}: StatusHistoryTimelineProps) {
  if (history.length === 0) {
    return (
      <Typography color="text.secondary" variant="body2">
        {emptyText}
      </Typography>
    )
  }

  return (
    <Stack spacing={1.75}>
      {history.map((item, index) => (
        <Box
          key={item.id}
          sx={{
            display: 'grid',
            gap: 1,
            gridTemplateColumns: '18px minmax(0, 1fr)',
          }}
        >
          <Box sx={{ alignItems: 'center', display: 'flex', flexDirection: 'column', pt: 0.5 }}>
            <Box
              sx={{
                bgcolor: 'primary.main',
                borderRadius: 99,
                boxShadow: '0 0 0 5px rgba(37, 99, 235, 0.08)',
                height: 8,
                width: 8,
              }}
            />
            {index < history.length - 1 && (
              <Box sx={{ bgcolor: 'divider', flexGrow: 1, mt: 0.75, width: 2 }} />
            )}
          </Box>

          <Box
            sx={{
              border: '1px solid',
              borderColor: 'divider',
              borderRadius: 2.5,
              p: 1.6,
            }}
          >
            <Stack
              direction={{ xs: 'column', sm: 'row' }}
              spacing={0.8}
              sx={{ alignItems: { sm: 'center' }, justifyContent: 'space-between' }}
            >
              <Stack direction="row" spacing={0.6} sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
                <Typography sx={{ fontWeight: 700 }} variant="body2">
                  {getTicketStatusLabel(item.oldStatus)}
                </Typography>
                <ArrowForward color="action" fontSize="small" />
                <Typography sx={{ fontWeight: 700 }} variant="body2">
                  {getTicketStatusLabel(item.newStatus)}
                </Typography>
              </Stack>

              <Typography color="text.secondary" variant="caption">
                {formatDate(item.changedAt)}
              </Typography>
            </Stack>

            <Typography color="text.secondary" sx={{ mt: 0.5 }} variant="caption">
              {item.changedByUserName}
            </Typography>

            {item.note && (
              <Typography sx={{ mt: 0.8, whiteSpace: 'pre-wrap' }} variant="body2">
                {item.note}
              </Typography>
            )}
          </Box>
        </Box>
      ))}
    </Stack>
  )
}
