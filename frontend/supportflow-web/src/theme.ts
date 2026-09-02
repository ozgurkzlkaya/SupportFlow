import { alpha, createTheme } from '@mui/material/styles'
import { TicketPriority, TicketStatus } from './types/ticket'

export const brandColors = {
  violetDeep: '#4C1D95',
  violetDark: '#5B21B6',
  violet: '#7C3AED',
  violetBright: '#8B5CF6',
  lavender: '#A78BFA',
  lavenderSurface: '#F6F0FF',
  indigoSurface: '#EEF2FF',
  amberSurface: '#FFF7ED',
  greenSurface: '#ECFDF3',
  roseSurface: '#FFF1F2',
}

export const brandGradients = {
  authPanel:
    'linear-gradient(158deg, rgba(76, 29, 149, 0.98) 0%, rgba(91, 33, 182, 0.98) 26%, rgba(124, 58, 237, 0.95) 64%, rgba(167, 139, 250, 0.9) 100%)',
  guidance:
    'linear-gradient(145deg, rgba(91, 33, 182, 1) 0%, rgba(124, 58, 237, 0.96) 58%, rgba(167, 139, 250, 0.9) 100%)',
  shell:
    'radial-gradient(circle at top left, rgba(124, 58, 237, 0.11), transparent 26%), radial-gradient(circle at top right, rgba(139, 92, 246, 0.08), transparent 22%), linear-gradient(180deg, #FBFAFF 0%, #F7F4FF 100%)',
}

export const metricCardTones = {
  violet: {
    accent: '#7C3AED',
    background: 'linear-gradient(180deg, rgba(245, 240, 255, 0.96), rgba(255,255,255,0.98))',
    border: alpha('#7C3AED', 0.16),
    iconBackground: 'rgba(124, 58, 237, 0.12)',
    highlight: 'rgba(124, 58, 237, 0.12)',
  },
  indigo: {
    accent: '#4F46E5',
    background: 'linear-gradient(180deg, rgba(238, 242, 255, 0.92), rgba(255,255,255,0.98))',
    border: alpha('#4F46E5', 0.16),
    iconBackground: 'rgba(79, 70, 229, 0.12)',
    highlight: 'rgba(79, 70, 229, 0.12)',
  },
  amber: {
    accent: '#B45309',
    background: 'linear-gradient(180deg, rgba(255, 247, 237, 0.96), rgba(255,255,255,0.98))',
    border: alpha('#B45309', 0.16),
    iconBackground: 'rgba(180, 83, 9, 0.12)',
    highlight: 'rgba(180, 83, 9, 0.12)',
  },
  green: {
    accent: '#15803D',
    background: 'linear-gradient(180deg, rgba(236, 253, 243, 0.96), rgba(255,255,255,0.98))',
    border: alpha('#15803D', 0.16),
    iconBackground: 'rgba(21, 128, 61, 0.12)',
    highlight: 'rgba(21, 128, 61, 0.12)',
  },
  rose: {
    accent: '#BE123C',
    background: 'linear-gradient(180deg, rgba(255, 241, 242, 0.96), rgba(255,255,255,0.98))',
    border: alpha('#BE123C', 0.14),
    iconBackground: 'rgba(190, 18, 60, 0.12)',
    highlight: 'rgba(190, 18, 60, 0.12)',
  },
} as const

export const ticketStatusColors: Record<TicketStatus, string> = {
  [TicketStatus.New]: '#7C3AED',
  [TicketStatus.Assigned]: '#4F46E5',
  [TicketStatus.InProgress]: '#2563EB',
  [TicketStatus.WaitingForCustomer]: '#B45309',
  [TicketStatus.Resolved]: '#15803D',
  [TicketStatus.Closed]: '#475569',
  [TicketStatus.Reopened]: '#BE123C',
}

export const ticketPriorityColors: Record<TicketPriority, string> = {
  [TicketPriority.Low]: '#64748B',
  [TicketPriority.Normal]: '#4F46E5',
  [TicketPriority.High]: '#C2410C',
  [TicketPriority.Critical]: '#BE123C',
}

export const appTheme = createTheme({
  palette: {
    mode: 'light',
    primary: {
      main: '#7c3aed',
      light: '#a78bfa',
      dark: '#5b21b6',
    },
    secondary: {
      main: '#4f46e5',
      light: '#818cf8',
      dark: '#3730a3',
    },
    success: {
      main: '#15803d',
    },
    warning: {
      main: '#b45309',
    },
    error: {
      main: '#b91c1c',
    },
    info: {
      main: '#2563eb',
    },
    text: {
      primary: '#0f172a',
      secondary: '#475569',
    },
    divider: alpha('#0f172a', 0.08),
    background: {
      default: '#f7f4ff',
      paper: '#ffffff',
    },
  },
  typography: {
    fontFamily:
      '"Segoe UI Variable", "Aptos", "Segoe UI", system-ui, -apple-system, BlinkMacSystemFont, sans-serif',
    h3: {
      fontSize: '2rem',
      fontWeight: 700,
      letterSpacing: '-0.035em',
    },
    h4: {
      fontSize: '1.7rem',
      fontWeight: 700,
      letterSpacing: '-0.03em',
    },
    h5: {
      fontSize: '1.25rem',
      fontWeight: 700,
      letterSpacing: '-0.02em',
    },
    h6: {
      fontSize: '1rem',
      fontWeight: 700,
      letterSpacing: '-0.015em',
    },
    button: {
      fontWeight: 600,
      letterSpacing: '-0.01em',
      textTransform: 'none',
    },
  },
  shape: {
    borderRadius: 12,
  },
  components: {
    MuiCssBaseline: {
      styleOverrides: {
        body: {
          backgroundAttachment: 'fixed',
          backgroundImage: brandGradients.shell,
        },
      },
    },
    MuiPaper: {
      defaultProps: {
        elevation: 0,
      },
      styleOverrides: {
        root: {
          backgroundImage: 'none',
          boxShadow: '0 16px 40px rgba(15, 23, 42, 0.05)',
        },
      },
    },
    MuiButton: {
      styleOverrides: {
        root: {
          borderRadius: 10,
          minHeight: 40,
          paddingInline: 16,
        },
      },
    },
    MuiChip: {
      styleOverrides: {
        root: {
          borderRadius: 999,
          fontWeight: 600,
        },
      },
    },
    MuiTextField: {
      defaultProps: {
        fullWidth: true,
      },
    },
    MuiOutlinedInput: {
      styleOverrides: {
        root: {
          backgroundColor: alpha('#ffffff', 0.92),
          borderRadius: 12,
        },
      },
    },
    MuiAlert: {
      styleOverrides: {
        root: {
          borderRadius: 12,
        },
      },
    },
    MuiDialog: {
      styleOverrides: {
        paper: {
          borderRadius: 16,
        },
      },
    },
    MuiTableCell: {
      styleOverrides: {
        head: {
          backgroundColor: '#F4EEFF',
          color: '#334155',
          fontWeight: 700,
        },
        root: {
          borderBottomColor: alpha('#0f172a', 0.08),
        },
      },
    },
    MuiTableRow: {
      styleOverrides: {
        root: {
          transition: 'background-color 120ms ease, box-shadow 120ms ease',
        },
      },
    },
  },
})
