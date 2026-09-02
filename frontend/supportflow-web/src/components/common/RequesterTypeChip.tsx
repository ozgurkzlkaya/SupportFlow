import { Chip, type ChipProps } from '@mui/material'

type RequesterType = 'Registered' | 'External' | 'RegisteredCustomer' | 'ExternalRequester'

const requesterPresentation: Record<RequesterType, { color: ChipProps['color']; label: string }> = {
  Registered: { color: 'primary', label: 'Kayıtlı Müşteri' },
  RegisteredCustomer: { color: 'primary', label: 'Kayıtlı Müşteri' },
  External: { color: 'warning', label: 'Harici Müşteri' },
  ExternalRequester: { color: 'warning', label: 'Harici Müşteri' },
}

interface RequesterTypeChipProps {
  requesterType: RequesterType
  size?: ChipProps['size']
}

export function RequesterTypeChip({ requesterType, size = 'small' }: RequesterTypeChipProps) {
  const presentation = requesterPresentation[requesterType]

  return <Chip color={presentation.color} label={presentation.label} size={size} variant="outlined" />
}
