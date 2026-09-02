import { alpha, Chip, type ChipProps } from '@mui/material'
import { UserRole, type UserRole as UserRoleType } from '../../types/auth'
import { getUserRoleLabel } from '../../utils/userLabels'

const roleColors: Record<UserRoleType, string> = {
  [UserRole.Admin]: '#5B21B6',
  [UserRole.TechnicalStaff]: '#2563EB',
  [UserRole.Customer]: '#475569',
}

interface UserRoleChipProps {
  role: UserRoleType
  size?: ChipProps['size']
}

export function UserRoleChip({ role, size = 'small' }: UserRoleChipProps) {
  const color = roleColors[role]

  return (
    <Chip
      label={getUserRoleLabel(role)}
      size={size}
      sx={{
        bgcolor: alpha(color, 0.11),
        borderColor: alpha(color, 0.3),
        color,
        fontWeight: 700,
      }}
      variant="outlined"
    />
  )
}
