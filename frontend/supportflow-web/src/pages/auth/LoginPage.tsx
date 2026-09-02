import { Visibility, VisibilityOff } from '@mui/icons-material'
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  IconButton,
  InputAdornment,
  Link as MuiLink,
  Paper,
  Stack,
  TextField,
  Typography,
} from '@mui/material'
import { useMemo, useState, type FormEvent } from 'react'
import { Link as RouterLink, Navigate, useLocation, useNavigate } from 'react-router-dom'
import supportflowIllustration from '../../assets/brand/supportflow-auth-illustration.svg'
import { SupportFlowLogo } from '../../components/brand/SupportFlowLogo'
import { useAuth } from '../../hooks/useAuth'
import { consumeSessionExpiredFlag } from '../../utils/authSession'
import { getFriendlyApiErrorMessage } from '../../utils/getApiErrorMessage'
import { getDefaultRouteForRole } from '../../utils/roleRouting'

const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

export function LoginPage() {
  const { user, loading, login } = useAuth()
  const location = useLocation()
  const navigate = useNavigate()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [showPassword, setShowPassword] = useState(false)
  const [emailError, setEmailError] = useState('')
  const [passwordError, setPasswordError] = useState('')
  const [loginError, setLoginError] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const sessionExpired = useMemo(
    () => location.search.includes('reason=session-expired') || consumeSessionExpiredFlag(),
    [location.search],
  )
  const registrationSucceeded = location.search.includes('registered=true')

  if (loading) {
    return (
      <Box sx={{ display: 'grid', minHeight: '100dvh', placeItems: 'center' }}>
        <CircularProgress aria-label="Oturum yükleniyor" />
      </Box>
    )
  }

  if (user) {
    return <Navigate replace to={getDefaultRouteForRole(user.role)} />
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    const normalizedEmail = email.trim()
    const nextEmailError = !normalizedEmail
      ? 'E-posta adresi gereklidir.'
      : emailPattern.test(normalizedEmail)
        ? ''
        : 'Geçerli bir e-posta adresi girin.'
    const nextPasswordError = password ? '' : 'Şifre gereklidir.'

    setEmailError(nextEmailError)
    setPasswordError(nextPasswordError)
    setLoginError('')

    if (nextEmailError || nextPasswordError) {
      return
    }

    setSubmitting(true)

    try {
      const response = await login({ email: normalizedEmail, password })
      navigate(getDefaultRouteForRole(response.user.role), { replace: true })
    } catch (error: unknown) {
      setLoginError(
        getFriendlyApiErrorMessage(error, {
          fallback: 'Giriş yapılamadı. Lütfen tekrar deneyin.',
          unauthorizedMessage: 'E-posta veya şifre hatalı.',
          unavailableMessage:
            'SupportFlow hizmetine ulaşılamıyor. Lütfen daha sonra tekrar deneyin.',
        }),
      )
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <Box
      component="main"
      sx={{
        alignItems: 'center',
        background:
          'radial-gradient(circle at 12% 18%, rgba(221, 214, 254, 0.22), transparent 24%), radial-gradient(circle at 82% 24%, rgba(255,255,255,0.14), transparent 20%), radial-gradient(circle at 72% 82%, rgba(196, 181, 253, 0.24), transparent 22%), linear-gradient(155deg, rgba(44, 14, 99, 0.98) 0%, rgba(76, 29, 149, 0.98) 26%, rgba(91, 33, 182, 0.96) 58%, rgba(124, 58, 237, 0.94) 100%)',
        display: 'flex',
        minHeight: '100dvh',
        overflow: 'hidden',
        position: 'relative',
        px: { xs: 2, sm: 3, lg: 5 },
        py: { xs: 2.5, sm: 3.5, lg: 4.5 },
      }}
    >
      <Box
        sx={{
          background:
            'radial-gradient(circle at top right, rgba(255,255,255,0.16), transparent 28%), radial-gradient(circle at 18% 86%, rgba(221,214,254,0.18), transparent 24%)',
          inset: 0,
          pointerEvents: 'none',
          position: 'absolute',
        }}
      />
      <Box
        sx={{
          background: {
            xs: 'linear-gradient(180deg, rgba(0,0,0,0.18) 0%, rgba(0,0,0,0.1) 36%, rgba(0,0,0,0.04) 100%)',
            lg: 'linear-gradient(90deg, rgba(0,0,0,0.35) 0%, rgba(0,0,0,0.3) 24%, rgba(0,0,0,0.2) 48%, rgba(0,0,0,0.12) 68%, rgba(0,0,0,0.05) 100%)',
          },
          inset: 0,
          pointerEvents: 'none',
          position: 'absolute',
        }}
      />
      <Box
        sx={{
          background: 'linear-gradient(180deg, rgba(255,255,255,0.05), transparent 22%)',
          border: '1px solid rgba(255,255,255,0.08)',
          borderRadius: { xs: 0, lg: 32 },
          inset: { xs: 0, lg: 20 },
          pointerEvents: 'none',
          position: 'absolute',
        }}
      />
      <Box
        aria-hidden="true"
        component="img"
        src={supportflowIllustration}
        sx={{
          bottom: { lg: -126, xl: -116 },
          display: { xs: 'none', lg: 'block' },
          filter: 'drop-shadow(0 34px 68px rgba(36, 11, 86, 0.34))',
          left: { lg: -150, xl: -108 },
          maxWidth: { lg: 760, xl: 860 },
          opacity: 0.76,
          pointerEvents: 'none',
          position: 'absolute',
          transform: 'translate3d(0, 0, 0)',
          width: { lg: '44%', xl: '46%' },
          zIndex: 0,
        }}
      />

      <Box
        sx={{
          alignItems: 'center',
          display: 'grid',
          gap: { xs: 2.5, lg: 3.5 },
          gridTemplateColumns: { xs: '1fr', lg: 'minmax(0, 1fr) minmax(420px, 0.78fr)' },
          margin: '0 auto',
          maxWidth: 1360,
          position: 'relative',
          width: '100%',
          zIndex: 1,
        }}
      >
        <Stack
          spacing={3}
          sx={{
            alignSelf: 'start',
            color: '#fff',
            display: { xs: 'none', lg: 'flex' },
            justifyContent: 'flex-start',
            maxWidth: 540,
            pb: { lg: 22, xl: 24 },
            pl: { lg: 0.5, xl: 1.5 },
            pr: { lg: 4.5, xl: 6.5 },
            pt: { lg: 2, xl: 3 },
            position: 'relative',
            zIndex: 1,
          }}
        >
          <SupportFlowLogo
            colorScheme="light"
            sx={{ width: 208 }}
          />

          <Box sx={{ maxWidth: 490 }}>
            <Typography
              sx={{
                fontSize: { lg: '3.05rem', xl: '3.45rem' },
                fontWeight: 900,
                letterSpacing: '-0.055em',
                lineHeight: 0.98,
                textShadow: '0 2px 12px rgba(0,0,0,0.25)',
              }}
            >
              Destek süreçlerini tek ekranda toplayın.
            </Typography>
            <Typography
              sx={{
                color: 'rgba(255,255,255,0.95)',
                fontSize: '1.02rem',
                lineHeight: 1.78,
                mt: 1.8,
                maxWidth: 430,
                textShadow: '0 1px 8px rgba(0,0,0,0.16)',
              }}
            >
              SupportFlow; müşteri taleplerini, teknik ekip aksiyonlarını ve Gmail destek akışını
              aynı operasyon alanında görünür tutar.
            </Typography>
          </Box>

          <Stack
            direction="row"
            spacing={1.2}
            sx={{ flexWrap: 'wrap', maxWidth: 440, position: 'relative', zIndex: 1 }}
          >
            {['Merkezi takip', 'Rol bazlı erişim', 'Gmail entegrasyonu'].map((item) => (
              <Box
                key={item}
                sx={{
                  backdropFilter: 'blur(14px)',
                  backgroundColor: 'rgba(255,255,255,0.14)',
                  border: '1px solid rgba(255,255,255,0.24)',
                  borderRadius: 999,
                  boxShadow: '0 12px 24px rgba(15, 6, 38, 0.12)',
                  px: 1.5,
                  py: 0.9,
                }}
              >
                <Typography
                  sx={{
                    color: 'rgba(255,255,255,0.98)',
                    fontWeight: 700,
                    textShadow: '0 1px 6px rgba(0,0,0,0.18)',
                  }}
                  variant="body2"
                >
                  {item}
                </Typography>
              </Box>
            ))}
          </Stack>
        </Stack>

        <Paper
          sx={{
            backdropFilter: 'blur(22px)',
            background:
              'linear-gradient(180deg, rgba(255,255,255,0.94), rgba(255,255,255,0.9))',
            border: '1px solid rgba(255,255,255,0.34)',
            borderRadius: { xs: 5, sm: 6 },
            boxShadow: '0 34px 80px rgba(19, 7, 48, 0.28)',
            justifySelf: { lg: 'end' },
            margin: { xs: 'auto', lg: '0' },
            maxWidth: 448,
            p: { xs: 2.75, sm: 3.4, lg: 3.6 },
            width: '100%',
          }}
        >
          <Stack spacing={2.6}>
            <Box>
              <SupportFlowLogo
                sx={{ width: { xs: 154, sm: 168 } }}
                width={168}
              />
            </Box>

            <Box>
              <Typography component="h1" sx={{ fontWeight: 800, letterSpacing: '-0.04em' }} variant="h4">
                Giriş Yap
              </Typography>
              <Typography color="text.secondary" sx={{ mt: 0.8, maxWidth: 340 }} variant="body2">
                Hesabınızla oturum açın ve çalışma alanınıza geçin.
              </Typography>
            </Box>

            <Box component="form" noValidate onSubmit={handleSubmit}>
              <Stack spacing={2}>
                {sessionExpired && !loginError && (
                  <Alert severity="info">
                    Oturumunuz sona erdi. Lütfen tekrar giriş yapın.
                  </Alert>
                )}

                {registrationSucceeded && !loginError && (
                  <Alert severity="success">
                    Kayıt başarılı. Hesabınızla giriş yapabilirsiniz.
                  </Alert>
                )}

                {loginError && <Alert severity="error">{loginError}</Alert>}

                <TextField
                  autoComplete="email"
                  autoFocus
                  error={Boolean(emailError)}
                  helperText={emailError}
                  id="email"
                  inputMode="email"
                  label="E-posta"
                  name="email"
                  onChange={(event) => {
                    setEmail(event.target.value)
                    setEmailError('')
                    setLoginError('')
                  }}
                  required
                  type="email"
                  value={email}
                />

                <TextField
                  autoComplete="current-password"
                  error={Boolean(passwordError)}
                  helperText={passwordError}
                  id="password"
                  label="Şifre"
                  name="password"
                  onChange={(event) => {
                    setPassword(event.target.value)
                    setPasswordError('')
                    setLoginError('')
                  }}
                  required
                  slotProps={{
                    input: {
                      endAdornment: (
                        <InputAdornment position="end">
                          <IconButton
                            aria-label={showPassword ? 'Şifreyi gizle' : 'Şifreyi göster'}
                            aria-pressed={showPassword}
                            edge="end"
                            onClick={() => setShowPassword((visible) => !visible)}
                            size="small"
                            type="button"
                          >
                            {showPassword ? (
                              <VisibilityOff fontSize="small" />
                            ) : (
                              <Visibility fontSize="small" />
                            )}
                          </IconButton>
                        </InputAdornment>
                      ),
                    },
                  }}
                  type={showPassword ? 'text' : 'password'}
                  value={password}
                />

                <Button
                  disabled={submitting}
                  size="large"
                  sx={{ boxShadow: '0 14px 28px rgba(91, 33, 182, 0.22)' }}
                  type="submit"
                  variant="contained"
                >
                  {submitting ? (
                    <>
                      <CircularProgress
                        aria-hidden="true"
                        color="inherit"
                        size={18}
                        sx={{ mr: 1 }}
                      />
                      Giriş yapılıyor...
                    </>
                  ) : (
                    'Giriş Yap'
                  )}
                </Button>
              </Stack>
            </Box>

            <Typography color="text.secondary" variant="body2">
              Hesabınız yok mu?{' '}
              <MuiLink component={RouterLink} to="/register" underline="hover">
                Müşteri hesabı oluşturun
              </MuiLink>
            </Typography>
          </Stack>
        </Paper>
      </Box>
    </Box>
  )
}
