import {
  AccessTimeOutlined,
  MarkEmailUnreadOutlined,
  PriorityHighOutlined,
  TimelapseOutlined,
} from '@mui/icons-material'
import { Box, List, ListItemButton, Paper, Stack, Typography } from '@mui/material'
import type { QueueAgingDashboard } from '../../types/dashboard'
import { formatDuration } from '../../utils/formatDuration'
import { MetricCard } from '../common/MetricCard'
import { QueueAgingChip } from '../common/QueueAgingChip'
import { SlaStateChip } from '../common/SlaStateChip'
import { TicketPriorityChip } from '../common/TicketPriorityChip'

interface QueueAgingDashboardSectionProps {
  dashboard: QueueAgingDashboard
  onOpenTicket: (ticketId: number) => void
}

export function QueueAgingDashboardSection({
  dashboard,
  onOpenTicket,
}: QueueAgingDashboardSectionProps) {
  return (
    <Stack spacing={2}>
      <Box>
        <Typography sx={{ fontWeight: 800 }} variant="h6">Operasyon Kuyruğu</Typography>
        <Typography color="text.secondary" variant="body2">
          Müşterinin ekip yanıtını beklediği aktif talepler.
        </Typography>
      </Box>

      <Box
        sx={{
          display: 'grid',
          gap: 2,
          gridTemplateColumns: {
            xs: '1fr',
            sm: 'repeat(2, minmax(0, 1fr))',
            lg: 'repeat(4, minmax(0, 1fr))',
          },
        }}
      >
        <MetricCard
          accentColor="#4f46e5"
          description="Aktif müşteri yanıtı bekleyen talepler"
          icon={<MarkEmailUnreadOutlined />}
          title="Yanıt Bekleyen"
          value={dashboard.awaitingReplyCount}
        />
        <MetricCard
          accentColor="#2563eb"
          description="En az iki saattir yanıt bekleyenler"
          icon={<AccessTimeOutlined />}
          title="2+ Saat Bekleyen"
          value={dashboard.waitingOver2HoursCount}
        />
        <MetricCard
          accentColor="#b45309"
          description="En az sekiz saattir yanıt bekleyenler"
          icon={<TimelapseOutlined />}
          title="8+ Saat Bekleyen"
          value={dashboard.waitingOver8HoursCount}
        />
        <MetricCard
          accentColor="#be123c"
          description="En az bir gündür yanıt bekleyenler"
          icon={<PriorityHighOutlined />}
          title="24+ Saat Bekleyen"
          value={dashboard.waitingOver24HoursCount}
        />
      </Box>

      <Paper sx={{ border: '1px solid', borderColor: 'divider' }}>
        <Box sx={{ borderBottom: '1px solid', borderColor: 'divider', p: 2.25 }}>
          <Typography sx={{ fontWeight: 800 }} variant="h6">En Uzun Bekleyen Talepler</Typography>
          <Typography color="text.secondary" variant="body2">
            Aktif yanıt bekleme süresine göre ilk beş talep.
          </Typography>
        </Box>

        {dashboard.oldestWaitingTickets.length === 0 ? (
          <Typography color="text.secondary" sx={{ p: 2.25 }} variant="body2">
            Yanıt bekleyen talep bulunmuyor.
          </Typography>
        ) : (
          <List disablePadding>
            {dashboard.oldestWaitingTickets.map((ticket, index) => (
              <ListItemButton
                divider={index < dashboard.oldestWaitingTickets.length - 1}
                key={ticket.ticketId}
                onClick={() => onOpenTicket(ticket.ticketId)}
                sx={{ alignItems: 'flex-start', gap: 1.5, px: 2.25, py: 1.65 }}
              >
                <Box sx={{ flexGrow: 1, minWidth: 0 }}>
                  <Typography color="text.secondary" variant="caption">{ticket.ticketNumber}</Typography>
                  <Typography sx={{ fontWeight: 700, mt: 0.3 }} variant="body2">{ticket.title}</Typography>
                  <Typography color="text.secondary" sx={{ mt: 0.5 }} variant="caption">
                    Atanan: {ticket.assignedUserName || 'Atanmadı'} · Bekleme: {formatDuration(ticket.awaitingStaffMinutes)}
                  </Typography>
                </Box>
                <Stack
                  direction={{ xs: 'column', sm: 'row' }}
                  spacing={0.65}
                  sx={{ alignItems: { xs: 'flex-end', sm: 'center' }, flexShrink: 0 }}
                >
                  <TicketPriorityChip priority={ticket.priority} />
                  <QueueAgingChip bucket={ticket.agingBucket} />
                  <SlaStateChip state={ticket.slaMostUrgentState} />
                </Stack>
              </ListItemButton>
            ))}
          </List>
        )}
      </Paper>
    </Stack>
  )
}
