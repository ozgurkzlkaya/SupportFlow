import { alpha, Chip, type ChipProps } from '@mui/material'
import type { TicketClassificationItem } from '../../types/taxonomy'

interface TicketCategoryChipProps {
  category: TicketClassificationItem | null
  size?: ChipProps['size']
}

export function TicketCategoryChip({ category, size = 'small' }: TicketCategoryChipProps) {
  const label = category
    ? `${category.name}${category.isActive ? '' : ' (Pasif)'}`
    : 'Sınıflandırılmamış'

  return (
    <Chip
      color={category?.isActive ? 'primary' : 'default'}
      label={label}
      size={size}
      sx={{
        bgcolor: category?.isActive ? (theme) => alpha(theme.palette.primary.main, 0.1) : 'transparent',
        fontWeight: 700,
        maxWidth: '100%',
      }}
      variant="outlined"
    />
  )
}
