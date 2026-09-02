import { alpha, Chip, type ChipProps } from '@mui/material'
import { ticketPriorityColors } from '../../theme'
import { TicketPriority } from '../../types/ticket'
import { getTicketPriorityLabel } from '../../utils/ticketLabels'

const priorityPresentation: Record<
  TicketPriority,
  { label: string; color: string; variant?: ChipProps['variant'] }
> = {
  [TicketPriority.Low]: {
    label: getTicketPriorityLabel(TicketPriority.Low),
    color: ticketPriorityColors.Low,
    variant: 'outlined',
  },
  [TicketPriority.Normal]: {
    label: getTicketPriorityLabel(TicketPriority.Normal),
    color: ticketPriorityColors.Normal,
  },
  [TicketPriority.High]: {
    label: getTicketPriorityLabel(TicketPriority.High),
    color: ticketPriorityColors.High,
  },
  [TicketPriority.Critical]: {
    label: getTicketPriorityLabel(TicketPriority.Critical),
    color: ticketPriorityColors.Critical,
  },
}

interface TicketPriorityChipProps {
  priority: TicketPriority
  size?: ChipProps['size']
}

export function TicketPriorityChip({
  priority,
  size = 'small',
}: TicketPriorityChipProps) {
  const presentation = priorityPresentation[priority]

  return (
    <Chip
      label={presentation.label}
      size={size}
      sx={{
        bgcolor:
          presentation.variant === 'outlined'
            ? 'transparent'
            : alpha(presentation.color, 0.12),
        borderColor: alpha(presentation.color, 0.32),
        color: presentation.color,
        fontWeight: 700,
      }}
      variant={presentation.variant ?? 'outlined'}
    />
  )
}
