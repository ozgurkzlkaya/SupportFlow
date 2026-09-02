import { Box, Button, Paper, Stack, Typography } from '@mui/material'
import type { ReactNode } from 'react'
import { SupportFlowMark } from '../brand/SupportFlowMark'

interface EmptyStateCardProps {
  icon?: ReactNode
  title: string
  description: string
  actionLabel?: string
  onAction?: () => void
}

export function EmptyStateCard({
  icon,
  title,
  description,
  actionLabel,
  onAction,
}: EmptyStateCardProps) {
  return (
    <Paper
      sx={{
        background: 'linear-gradient(180deg, rgba(245, 240, 255, 0.9), rgba(255,255,255,0.98))',
        border: '1px dashed',
        borderColor: 'divider',
        p: { xs: 3, sm: 3.5 },
        textAlign: 'center',
      }}
    >
      <Stack spacing={1.25} sx={{ alignItems: 'center', mx: 'auto', maxWidth: 460 }}>
        <SupportFlowMark size={40} sx={{ opacity: 0.94 }} />
        {icon && (
          <Box
            sx={{
              alignItems: 'center',
              bgcolor: 'rgba(124, 58, 237, 0.08)',
              borderRadius: 99,
              color: 'primary.main',
              display: 'grid',
              height: 48,
              placeItems: 'center',
              width: 48,
            }}
          >
            {icon}
          </Box>
        )}
        <Typography sx={{ fontWeight: 700 }} variant="h6">
          {title}
        </Typography>
        <Typography color="text.secondary" variant="body2">
          {description}
        </Typography>
        {actionLabel && onAction && (
          <Button onClick={onAction} sx={{ mt: 0.5 }} variant="outlined">
            {actionLabel}
          </Button>
        )}
      </Stack>
    </Paper>
  )
}
