import { Chip, type ChipProps } from '@mui/material'
import type { TicketClassificationItem } from '../../types/taxonomy'

interface TicketTagChipProps {
  tag: TicketClassificationItem
  size?: ChipProps['size']
}

export function TicketTagChip({ tag, size = 'small' }: TicketTagChipProps) {
  return (
    <Chip
      label={`${tag.name}${tag.isActive ? '' : ' (Pasif)'}`}
      size={size}
      sx={{ fontWeight: 650, maxWidth: '100%' }}
      variant="outlined"
    />
  )
}
