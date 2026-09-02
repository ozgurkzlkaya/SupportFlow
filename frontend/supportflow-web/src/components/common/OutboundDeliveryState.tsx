import { Alert, Button, Chip, CircularProgress, Stack, Typography } from '@mui/material'
import { DeliveryStatus } from '../../types/ticket'
import { getDeliveryFailureMessage } from '../../utils/deliveryPresentation'

interface OutboundDeliveryStateProps {
  attemptCount?: number
  deliveryStatus?: DeliveryStatus
  onRetry?: () => void
  retrying?: boolean
  safeDeliveryError?: string | null
}

export function OutboundDeliveryState({
  attemptCount = 0,
  deliveryStatus,
  onRetry,
  retrying = false,
  safeDeliveryError,
}: OutboundDeliveryStateProps) {
  if (deliveryStatus === DeliveryStatus.Pending) {
    return <Chip label="E-posta gönderiliyor..." size="small" sx={{ alignSelf: 'flex-start' }} variant="outlined" />
  }

  if (deliveryStatus !== DeliveryStatus.Failed) {
    return null
  }

  return (
    <Alert
      action={
        onRetry ? (
          <Button color="inherit" disabled={retrying} onClick={onRetry} size="small">
            {retrying ? <CircularProgress color="inherit" size={16} /> : 'Yeniden Dene'}
          </Button>
        ) : undefined
      }
      severity="error"
    >
      <Stack direction="row" spacing={0.75} sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
        <Typography variant="body2">{getDeliveryFailureMessage(safeDeliveryError)}</Typography>
        {attemptCount > 0 ? <Chip label={`Deneme: ${attemptCount}`} size="small" variant="outlined" /> : null}
      </Stack>
    </Alert>
  )
}
