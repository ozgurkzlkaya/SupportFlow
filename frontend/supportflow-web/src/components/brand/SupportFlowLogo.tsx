import { Box, Stack, Typography, type SxProps, type Theme } from '@mui/material'
import { SupportFlowMark } from './SupportFlowMark'

interface SupportFlowLogoProps {
  markOnly?: boolean
  width?: number | string
  colorScheme?: 'light' | 'dark'
  sx?: SxProps<Theme>
}

export function SupportFlowLogo({
  markOnly = false,
  width = markOnly ? 44 : 168,
  colorScheme = 'dark',
  sx,
}: SupportFlowLogoProps) {
  if (markOnly) {
    return <SupportFlowMark size={width} sx={sx} />
  }

  const supportColor = colorScheme === 'light' ? 'rgba(255,255,255,0.96)' : '#140F29'
  const flowColor = colorScheme === 'light' ? '#DDD6FE' : '#6D28D9'

  return (
    <Stack
      direction="row"
      spacing={1.2}
      sx={[
        {
          alignItems: 'center',
          display: 'inline-flex',
          minWidth: 0,
          width,
        },
        ...(Array.isArray(sx) ? sx : [sx]),
      ]}
    >
      <SupportFlowMark
        size={44}
        sx={{
          filter:
            colorScheme === 'light'
              ? 'drop-shadow(0 12px 20px rgba(22, 8, 54, 0.22))'
              : 'drop-shadow(0 12px 20px rgba(109, 40, 217, 0.14))',
        }}
      />
      <Typography
        aria-label="SupportFlow"
        component="span"
        sx={{
          fontSize: '1.45rem',
          fontWeight: 900,
          letterSpacing: '-0.045em',
          lineHeight: 1,
          whiteSpace: 'nowrap',
        }}
      >
        <Box component="span" sx={{ color: supportColor }}>
          Support
        </Box>
        <Box component="span" sx={{ color: flowColor }}>
          Flow
        </Box>
      </Typography>
    </Stack>
  )
}
