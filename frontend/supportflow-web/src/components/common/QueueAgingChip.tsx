import { alpha, Chip } from '@mui/material'
import { QueueAgingBucket } from '../../types/ticket'
import { getQueueAgingLabel } from '../../utils/queueAgingLabels'

const presentation: Record<QueueAgingBucket, { color: string }> = {
  [QueueAgingBucket.New]: { color: '#64748b' },
  [QueueAgingBucket.ShortWait]: { color: '#475569' },
  [QueueAgingBucket.Waiting]: { color: '#4f46e5' },
  [QueueAgingBucket.LongWait]: { color: '#b45309' },
  [QueueAgingBucket.Overdue]: { color: '#c2410c' },
  [QueueAgingBucket.CriticalWait]: { color: '#be123c' },
}

interface QueueAgingChipProps {
  bucket: QueueAgingBucket
}

export function QueueAgingChip({ bucket }: QueueAgingChipProps) {
  const item = presentation[bucket]

  return (
    <Chip
      label={getQueueAgingLabel(bucket)}
      size="small"
      sx={{
        bgcolor: alpha(item.color, 0.09),
        borderColor: alpha(item.color, 0.28),
        color: item.color,
        fontWeight: 700,
      }}
      variant="outlined"
    />
  )
}
