import { Box, Chip, Paper, Stack, Typography, type SxProps, type Theme } from '@mui/material'
import type { ReactNode } from 'react'
import { metricCardTones } from '../../theme'

type MetricCardTone = (typeof metricCardTones)[keyof typeof metricCardTones]

interface MetricCardProps {
  icon: ReactNode
  title: string
  value: number | string
  description?: string
  tone?: MetricCardTone
  accentColor?: string
  subtleLabel?: string
  sx?: SxProps<Theme>
}

export function MetricCard({
  icon,
  title,
  value,
  description,
  tone = metricCardTones.indigo,
  accentColor,
  subtleLabel,
  sx,
}: MetricCardProps) {
  const resolvedTone = accentColor
    ? {
        accent: accentColor,
        background: `linear-gradient(180deg, ${accentColor}10, rgba(255,255,255,0.98))`,
        border: `${accentColor}24`,
        iconBackground: `${accentColor}14`,
        highlight: `${accentColor}1F`,
      }
    : tone

  return (
    <Paper
      sx={[
        {
          background: resolvedTone.background,
          border: '1px solid',
          borderColor: resolvedTone.border,
          overflow: 'hidden',
          p: 2,
          position: 'relative',
          transition: 'transform 120ms ease, box-shadow 120ms ease',
          '&::before': {
            background: `linear-gradient(90deg, ${resolvedTone.accent}, transparent)`,
            content: '""',
            height: 4,
            left: 0,
            position: 'absolute',
            right: 0,
            top: 0,
          },
          '&:hover': {
            boxShadow: '0 18px 34px rgba(15, 23, 42, 0.07)',
            transform: 'translateY(-1px)',
          },
        },
        ...(Array.isArray(sx) ? sx : [sx]),
      ]}
    >
      <Stack spacing={1.5}>
        <Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between' }}>
          <Box
            sx={{
              alignItems: 'center',
              bgcolor: resolvedTone.iconBackground,
              borderRadius: 2.5,
              boxShadow: `inset 0 0 0 1px ${resolvedTone.highlight}`,
              color: resolvedTone.accent,
              display: 'grid',
              height: 42,
              placeItems: 'center',
              width: 42,
            }}
          >
            {icon}
          </Box>
          {subtleLabel && (
            <Chip
              label={subtleLabel}
              size="small"
              sx={{ borderColor: 'divider', fontWeight: 600 }}
              variant="outlined"
            />
          )}
        </Stack>

        <Box>
          <Typography color="text.secondary" sx={{ fontWeight: 600 }} variant="caption">
            {title}
          </Typography>
          <Typography sx={{ fontWeight: 800, letterSpacing: '-0.03em', mt: 0.5 }} variant="h4">
            {value}
          </Typography>
        </Box>

        {description && (
          <Typography color="text.secondary" variant="caption">
            {description}
          </Typography>
        )}
      </Stack>
    </Paper>
  )
}
