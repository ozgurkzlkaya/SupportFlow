import { alpha, Chip, type ChipProps } from '@mui/material'

interface UserAccountStatusChipProps {
  isActive: boolean
  size?: ChipProps['size']
}

export function UserAccountStatusChip({ isActive, size = 'small' }: UserAccountStatusChipProps) {
  const color = isActive ? '#15803D' : '#64748B'

  return (
    <Chip
      label={isActive ? 'Aktif' : 'Pasif'}
      size={size}
      sx={{
        bgcolor: alpha(color, 0.12),
        borderColor: alpha(color, 0.32),
        color,
        fontWeight: 700,
      }}
      variant="outlined"
    />
  )
}
