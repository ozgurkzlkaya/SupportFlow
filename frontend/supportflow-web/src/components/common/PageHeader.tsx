import { Box, Stack, Typography, type SxProps, type Theme } from '@mui/material'
import type { ReactNode } from 'react'

interface PageHeaderProps {
  eyebrow?: string
  title: string
  description?: string
  actions?: ReactNode
  sx?: SxProps<Theme>
}

export function PageHeader({
  eyebrow,
  title,
  description,
  actions,
  sx,
}: PageHeaderProps) {
  return (
    <Box
      sx={[
        {
          alignItems: { md: 'flex-end' },
          display: 'flex',
          flexDirection: { xs: 'column', md: 'row' },
          gap: 2,
          justifyContent: 'space-between',
        },
        ...(Array.isArray(sx) ? sx : [sx]),
      ]}
    >
      <Stack spacing={0.9} sx={{ minWidth: 0 }}>
        {eyebrow && (
          <Typography
            color="text.secondary"
            sx={{ fontWeight: 700, letterSpacing: '0.08em', textTransform: 'uppercase' }}
            variant="caption"
          >
            {eyebrow}
          </Typography>
        )}
        <Typography
          component="h1"
          sx={{ fontWeight: 800, letterSpacing: '-0.03em' }}
          variant="h3"
        >
          {title}
        </Typography>
        {description && (
          <Typography color="text.secondary" sx={{ maxWidth: 760 }} variant="body1">
            {description}
          </Typography>
        )}
      </Stack>

      {actions && <Box sx={{ flexShrink: 0, width: { xs: '100%', md: 'auto' } }}>{actions}</Box>}
    </Box>
  )
}
