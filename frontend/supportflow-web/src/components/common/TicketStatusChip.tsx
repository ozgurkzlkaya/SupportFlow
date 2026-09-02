import { alpha, Chip, type ChipProps } from '@mui/material'
import { ticketStatusColors } from '../../theme'
import { TicketStatus } from '../../types/ticket'
import { getTicketStatusLabel } from '../../utils/ticketLabels'

const statusPresentation: Record<
  TicketStatus,
  { label: string; color: string; variant?: ChipProps['variant'] }
> = {
  [TicketStatus.New]: { label: getTicketStatusLabel(TicketStatus.New), color: ticketStatusColors.New },
  [TicketStatus.Assigned]: {
    label: getTicketStatusLabel(TicketStatus.Assigned),
    color: ticketStatusColors.Assigned,
  },
  [TicketStatus.InProgress]: {
    label: getTicketStatusLabel(TicketStatus.InProgress),
    color: ticketStatusColors.InProgress,
  },
  [TicketStatus.WaitingForCustomer]: {
    label: getTicketStatusLabel(TicketStatus.WaitingForCustomer),
    color: ticketStatusColors.WaitingForCustomer,
  },
  [TicketStatus.Resolved]: {
    label: getTicketStatusLabel(TicketStatus.Resolved),
    color: ticketStatusColors.Resolved,
  },
  [TicketStatus.Closed]: {
    label: getTicketStatusLabel(TicketStatus.Closed),
    color: ticketStatusColors.Closed,
    variant: 'outlined',
  },
  [TicketStatus.Reopened]: {
    label: getTicketStatusLabel(TicketStatus.Reopened),
    color: ticketStatusColors.Reopened,
  },
}

interface TicketStatusChipProps {
  status: TicketStatus
  size?: ChipProps['size']
}

export function TicketStatusChip({ status, size = 'small' }: TicketStatusChipProps) {
  const presentation = statusPresentation[status]

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
