import { alpha, Chip, type ChipProps } from '@mui/material'
import { SlaState } from '../../types/ticket'

const presentation: Record<SlaState, { label: string; color: string }> = {
  [SlaState.NotTracked]: { label: 'SLA Takip Dışı', color: '#64748b' },
  [SlaState.OnTrack]: { label: 'Zamanında', color: '#15803d' },
  [SlaState.DueSoon]: { label: 'SLA Yaklaşıyor', color: '#c2410c' },
  [SlaState.Breached]: { label: 'SLA İhlali', color: '#dc2626' },
  [SlaState.Met]: { label: 'SLA Karşılandı', color: '#047857' },
}

interface SlaStateChipProps {
  state: SlaState
  size?: ChipProps['size']
}

export function SlaStateChip({ state, size = 'small' }: SlaStateChipProps) {
  const item = presentation[state]

  return (
    <Chip
      label={item.label}
      size={size}
      sx={{
        bgcolor: alpha(item.color, state === SlaState.NotTracked ? 0.08 : 0.12),
        borderColor: alpha(item.color, 0.32),
        color: item.color,
        fontWeight: 700,
      }}
      variant="outlined"
    />
  )
}
