import { CheckCircleOutlined } from '@mui/icons-material'
import { Box, Stack, Typography } from '@mui/material'
import supportflowIllustration from '../../assets/brand/supportflow-auth-illustration.svg'
import { brandGradients } from '../../theme'
import { SupportFlowLogo } from './SupportFlowLogo'

const featureRows = [
  'Merkezi talep yönetimi',
  'Teknik ekip iş akışı',
  'Gmail destek entegrasyonu',
]

export function AuthBrandPanel() {
  return (
    <Box
      sx={{
        background: brandGradients.authPanel,
        color: '#fff',
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        minHeight: { lg: 640 },
        overflow: 'hidden',
        p: { xs: 3, sm: 3.5, lg: 4.5 },
        position: 'relative',
      }}
    >
      <Box
        sx={{
          background:
            'radial-gradient(circle at top right, rgba(255,255,255,0.24), transparent 32%), radial-gradient(circle at 16% 78%, rgba(221,214,254,0.22), transparent 30%)',
          inset: 0,
          position: 'absolute',
        }}
      />
      <Box
        sx={{
          background: 'linear-gradient(180deg, rgba(255,255,255,0.06), transparent 54%)',
          border: '1px solid rgba(255,255,255,0.08)',
          borderRadius: 6,
          inset: 18,
          pointerEvents: 'none',
          position: 'absolute',
        }}
      />

      <Stack
        spacing={3}
        sx={{
          flexGrow: 1,
          justifyContent: 'space-between',
          position: 'relative',
          zIndex: 1,
        }}
      >
        <Stack spacing={3} sx={{ maxWidth: 540 }}>
          <SupportFlowLogo
            colorScheme="light"
            sx={{ width: { xs: 170, lg: 196 } }}
          />

          <Box sx={{ maxWidth: 520 }}>
            <Typography sx={{ fontWeight: 800, letterSpacing: '-0.04em' }} variant="h3">
              Destek operasyonunuzu tek noktadan yönetin.
            </Typography>
            <Typography sx={{ color: 'rgba(255,255,255,0.82)', mt: 1.5 }} variant="body1">
              Talepler, müşteri iletişimi, teknik ekip aksiyonları ve Gmail destek akışı tek
              ürün içinde birlikte görünür kalır.
            </Typography>
          </Box>

          <Stack spacing={1.1} sx={{ maxWidth: 420 }}>
            {featureRows.map((item) => (
              <Stack
                direction="row"
                key={item}
                spacing={1.25}
                sx={{
                  alignItems: 'center',
                  backdropFilter: 'blur(12px)',
                  backgroundColor: 'rgba(255,255,255,0.1)',
                  border: '1px solid rgba(255,255,255,0.16)',
                  borderRadius: 2.75,
                  px: 1.5,
                  py: 1.2,
                }}
              >
                <CheckCircleOutlined fontSize="small" />
                <Typography variant="body2">{item}</Typography>
              </Stack>
            ))}
          </Stack>
        </Stack>

        <Box
          alt="SupportFlow kontrol paneli görseli"
          component="img"
          src={supportflowIllustration}
          sx={{
            bottom: { md: -70, lg: -92 },
            display: { xs: 'none', md: 'block' },
            maxWidth: { md: 560, lg: 640 },
            opacity: 0.98,
            pointerEvents: 'none',
            position: 'absolute',
            right: { md: -56, lg: -78 },
            width: { md: '78%', lg: '82%' },
          }}
        />
      </Stack>
    </Box>
  )
}
