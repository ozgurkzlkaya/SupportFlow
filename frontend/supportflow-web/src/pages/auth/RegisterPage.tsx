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
import { useState, type FormEvent } from 'react'
import { Link as RouterLink, Navigate, useNavigate } from 'react-router-dom'
import { AuthBrandPanel } from '../../components/brand/AuthBrandPanel'
import { SupportFlowLogo } from '../../components/brand/SupportFlowLogo'
import { useAuth } from '../../hooks/useAuth'
import { register } from '../../services/authService'
import { getFriendlyApiErrorMessage } from '../../utils/getApiErrorMessage'
import { getDefaultRouteForRole } from '../../utils/roleRouting'

const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

export function RegisterPage() {
  const { user, loading } = useAuth()
  const navigate = useNavigate()
  const [firstName, setFirstName] = useState('')
  const [lastName, setLastName] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [passwordConfirmation, setPasswordConfirmation] = useState('')
  const [showPassword, setShowPassword] = useState(false)
  const [showPasswordConfirmation, setShowPasswordConfirmation] = useState(false)
  const [firstNameError, setFirstNameError] = useState('')
  const [lastNameError, setLastNameError] = useState('')
  const [emailError, setEmailError] = useState('')
  const [passwordError, setPasswordError] = useState('')
  const [passwordConfirmationError, setPasswordConfirmationError] = useState('')
  const [submitError, setSubmitError] = useState('')
  const [submitting, setSubmitting] = useState(false)

  if (loading) {
    return (
      <Box sx={{ display: 'grid', minHeight: '100dvh', placeItems: 'center' }}>
        <CircularProgress aria-label="Kayıt sayfası yükleniyor" />
      </Box>
    )
  }

  if (user) {
    return <Navigate replace to={getDefaultRouteForRole(user.role)} />
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    const normalizedFirstName = firstName.trim()
    const normalizedLastName = lastName.trim()
    const normalizedEmail = email.trim()

    const nextFirstNameError = normalizedFirstName ? '' : 'Ad alanı gereklidir.'
    const nextLastNameError = normalizedLastName ? '' : 'Soyad alanı gereklidir.'
    const nextEmailError = !normalizedEmail
      ? 'E-posta adresi gereklidir.'
      : emailPattern.test(normalizedEmail)
        ? ''
        : 'Geçerli bir e-posta adresi girin.'
    const nextPasswordError = !password
      ? 'Şifre gereklidir.'
      : password.length >= 8
        ? ''
        : 'Şifre en az 8 karakter olmalıdır.'
    const nextPasswordConfirmationError = !passwordConfirmation
      ? 'Şifre tekrarı gereklidir.'
      : passwordConfirmation === password
        ? ''
        : 'Şifreler eşleşmiyor.'

    setFirstNameError(nextFirstNameError)
    setLastNameError(nextLastNameError)
    setEmailError(nextEmailError)
    setPasswordError(nextPasswordError)
    setPasswordConfirmationError(nextPasswordConfirmationError)
    setSubmitError('')

    if (
      nextFirstNameError ||
      nextLastNameError ||
      nextEmailError ||
      nextPasswordError ||
      nextPasswordConfirmationError
    ) {
      return
    }

    setSubmitting(true)

    try {
      await register({
        firstName: normalizedFirstName,
        lastName: normalizedLastName,
        email: normalizedEmail,
        password,
        companyId: null,
      })

      navigate('/login?registered=true', { replace: true })
    } catch (error: unknown) {
      setSubmitError(
        getFriendlyApiErrorMessage(error, {
          fallback: 'Kayıt oluşturulamadı. Lütfen tekrar deneyin.',
          conflictMessage: 'Bu e-posta adresi zaten kayıtlı.',
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
        minHeight: '100dvh',
        px: { xs: 2, sm: 3, lg: 4 },
        py: { xs: 3, sm: 4, lg: 5 },
      }}
    >
      <Box
        sx={{
          display: 'grid',
          gap: 2.5,
          gridTemplateColumns: { xs: '1fr', lg: 'minmax(0, 0.98fr) minmax(440px, 0.8fr)' },
          margin: '0 auto',
          maxWidth: 1220,
        }}
      >
        <Paper sx={{ display: { xs: 'none', lg: 'block' }, height: '100%' }}>
          <AuthBrandPanel />
        </Paper>

        <Paper
          sx={{
            border: '1px solid',
            borderColor: 'divider',
            p: { xs: 3, sm: 4, lg: 4.5 },
          }}
        >
          <Stack spacing={3}>
            <Box sx={{ display: { xs: 'block', lg: 'none' } }}>
              <SupportFlowLogo width={154} />
            </Box>

            <Box>
              <Typography component="h1" sx={{ fontWeight: 800 }} variant="h4">
                Müşteri Hesabı Oluşturun
              </Typography>
              <Typography color="text.secondary" sx={{ mt: 1 }} variant="body1">
                Yeni destek talepleri açmak ve mevcut taleplerinizi izlemek için hesabınızı
                oluşturun.
              </Typography>
            </Box>

            <Box component="form" noValidate onSubmit={handleSubmit}>
              <Stack spacing={2.5}>
                {submitError && <Alert severity="error">{submitError}</Alert>}

                <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
                  <TextField
                    autoComplete="given-name"
                    autoFocus
                    error={Boolean(firstNameError)}
                    helperText={firstNameError}
                    label="Ad"
                    name="firstName"
                    onChange={(event) => {
                      setFirstName(event.target.value)
                      setFirstNameError('')
                      setSubmitError('')
                    }}
                    required
                    value={firstName}
                  />

                  <TextField
                    autoComplete="family-name"
                    error={Boolean(lastNameError)}
                    helperText={lastNameError}
                    label="Soyad"
                    name="lastName"
                    onChange={(event) => {
                      setLastName(event.target.value)
                      setLastNameError('')
                      setSubmitError('')
                    }}
                    required
                    value={lastName}
                  />
                </Stack>

                <TextField
                  autoComplete="email"
                  error={Boolean(emailError)}
                  helperText={emailError}
                  inputMode="email"
                  label="E-posta"
                  name="email"
                  onChange={(event) => {
                    setEmail(event.target.value)
                    setEmailError('')
                    setSubmitError('')
                  }}
                  required
                  type="email"
                  value={email}
                />

                <TextField
                  autoComplete="new-password"
                  error={Boolean(passwordError)}
                  helperText={passwordError || 'En az 8 karakter olmalıdır.'}
                  label="Şifre"
                  name="password"
                  onChange={(event) => {
                    setPassword(event.target.value)
                    setPasswordError('')
                    setPasswordConfirmationError('')
                    setSubmitError('')
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
                            type="button"
                          >
                            {showPassword ? <VisibilityOff /> : <Visibility />}
                          </IconButton>
                        </InputAdornment>
                      ),
                    },
                  }}
                  type={showPassword ? 'text' : 'password'}
                  value={password}
                />

                <TextField
                  autoComplete="new-password"
                  error={Boolean(passwordConfirmationError)}
                  helperText={passwordConfirmationError}
                  label="Şifre Tekrarı"
                  name="passwordConfirmation"
                  onChange={(event) => {
                    setPasswordConfirmation(event.target.value)
                    setPasswordConfirmationError('')
                    setSubmitError('')
                  }}
                  required
                  slotProps={{
                    input: {
                      endAdornment: (
                        <InputAdornment position="end">
                          <IconButton
                            aria-label={
                              showPasswordConfirmation
                                ? 'Şifre tekrarını gizle'
                                : 'Şifre tekrarını göster'
                            }
                            aria-pressed={showPasswordConfirmation}
                            edge="end"
                            onClick={() =>
                              setShowPasswordConfirmation((visible) => !visible)
                            }
                            type="button"
                          >
                            {showPasswordConfirmation ? <VisibilityOff /> : <Visibility />}
                          </IconButton>
                        </InputAdornment>
                      ),
                    },
                  }}
                  type={showPasswordConfirmation ? 'text' : 'password'}
                  value={passwordConfirmation}
                />

                <Button disabled={submitting} size="large" type="submit" variant="contained">
                  {submitting ? (
                    <>
                      <CircularProgress
                        aria-hidden="true"
                        color="inherit"
                        size={20}
                        sx={{ mr: 1 }}
                      />
                      Hesap oluşturuluyor...
                    </>
                  ) : (
                    'Kayıt Ol'
                  )}
                </Button>
              </Stack>
            </Box>

            <Typography color="text.secondary" variant="body2">
              Zaten hesabınız var mı?{' '}
              <MuiLink component={RouterLink} to="/login" underline="hover">
                Giriş yapın
              </MuiLink>
            </Typography>
          </Stack>
        </Paper>
      </Box>
    </Box>
  )
}
