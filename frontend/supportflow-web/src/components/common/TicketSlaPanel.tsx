import { Box, Divider, Paper, Stack, Typography } from '@mui/material'
import type { TicketSla, TicketSlaMetric } from '../../types/ticket'
import { formatDate } from '../../utils/formatDate'
import { formatDuration } from '../../utils/formatDuration'
import { SlaStateChip } from './SlaStateChip'

function SlaMetric({ label, metric }: { label: string; metric: TicketSlaMetric }) {
  return (
    <Stack spacing={0.85}>
      <Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between' }}>
        <Typography sx={{ fontWeight: 700 }} variant="body2">{label}</Typography>
        <SlaStateChip state={metric.state} />
      </Stack>
      <Typography color="text.secondary" variant="caption">
        Son tarih: {formatDate(metric.dueAt)}
      </Typography>
      {metric.completedAt ? (
        <Typography color="text.secondary" variant="caption">
          Tamamlandı: {formatDate(metric.completedAt)}
        </Typography>
      ) : null}
      {metric.remainingMinutes !== null ? (
        <Typography color="text.secondary" variant="caption">
          Kalan: {formatDuration(metric.remainingMinutes)}
        </Typography>
      ) : null}
      {metric.overdueMinutes !== null ? (
        <Typography color="error.main" variant="caption">
          SLA {formatDuration(metric.overdueMinutes)} aşıldı
        </Typography>
      ) : null}
    </Stack>
  )
}

export function TicketSlaPanel({ sla }: { sla: TicketSla }) {
  return (
    <Paper sx={{ border: '1px solid', borderColor: 'divider', p: 3 }}>
      <Typography sx={{ fontWeight: 700, mb: 2 }} variant="h6">SLA Durumu</Typography>
      {!sla.isTracked ? (
        <Stack spacing={1.25}>
          <Box><SlaStateChip state="NotTracked" /></Box>
          <Typography color="text.secondary" variant="body2">
            Bu talep SLA takibi etkinleştirilmeden önce oluşturuldu.
          </Typography>
        </Stack>
      ) : (
        <Stack divider={<Divider flexItem />} spacing={2.25}>
          <SlaMetric label="İlk Yanıt" metric={sla.firstResponse} />
          <SlaMetric label="Çözüm" metric={sla.resolution} />
        </Stack>
      )}
    </Paper>
  )
}
