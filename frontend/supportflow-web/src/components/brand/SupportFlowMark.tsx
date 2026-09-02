import { Box, type SxProps, type Theme } from '@mui/material'
import supportflowMark from '../../assets/brand/supportflow-mark.svg'

interface SupportFlowMarkProps {
  size?: number | string
  sx?: SxProps<Theme>
}

export function SupportFlowMark({
  size = 42,
  sx,
}: SupportFlowMarkProps) {
  return (
    <Box
      alt="SupportFlow işareti"
      component="img"
      src={supportflowMark}
      sx={[
        {
          display: 'block',
          flexShrink: 0,
          height: size,
          width: size,
        },
        ...(Array.isArray(sx) ? sx : [sx]),
      ]}
    />
  )
}
