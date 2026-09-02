import {
  LockOutlined,
  Visibility,
  VisibilityOff,
} from '@mui/icons-material'
import {
  Alert,
  Avatar,
  Box,
  Button,
  CircularProgress,
  IconButton,
  InputAdornment,
  Paper,
  Snackbar,
  Stack,
  TextField,
  Typography,
} from '@mui/material'
import { useEffect, useMemo, useRef, useState, type FormEvent } from 'react'
import { PageHeader } from '../../components/common/PageHeader'
import { SupportFlowLogo } from '../../components/brand/SupportFlowLogo'
import { useAuth } from '../../hooks/useAuth'
import { changeMyPassword, getMyProfile, updateMyProfile } from '../../services/profileService'
import type { UserProfile } from '../../types/profile'
import { formatDate } from '../../utils/formatDate'
import { getFriendlyApiErrorMessage } from '../../utils/getApiErrorMessage'
import { getUserRoleLabel } from '../../utils/userLabels'

export function ProfilePage() {
  const { refreshUser } = useAuth()
  const [profile, setProfile] = useState<UserProfile | null>(null)
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState('')
  const [profileError, setProfileError] = useState('')
  const [passwordError, setPasswordError] = useState('')
  const [snackbarMessage, setSnackbarMessage] = useState('')
  const [savingProfile, setSavingProfile] = useState(false)
  const [changingPassword, setChangingPassword] = useState(false)
  const [firstName, setFirstName] = useState('')
  const [lastName, setLastName] = useState('')
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirmNewPassword, setConfirmNewPassword] = useState('')
  const [showCurrentPassword, setShowCurrentPassword] = useState(false)
  const [showNewPassword, setShowNewPassword] = useState(false)
  const [showConfirmNewPassword, setShowConfirmNewPassword] = useState(false)
  const passwordSectionRef = useRef<HTMLFormElement | null>(null)

  useEffect(() => {
    let active = true

    async function loadProfile() {
      setLoading(true)
      setLoadError('')

      try {
        const response = await getMyProfile()
        if (active) {
          setProfile(response)
          setFirstName(response.firstName)
          setLastName(response.lastName)
        }
      } catch (error: unknown) {
        if (active) {
          setLoadError(
            getFriendlyApiErrorMessage(error, {
              fallback: 'Profil bilgileri yüklenemedi. Lütfen tekrar deneyin.',
              unavailableMessage:
                'SupportFlow hizmetine ulaşılamıyor. Lütfen daha sonra tekrar deneyin.',
            }),
          )
        }
      } finally {
        if (active) {
          setLoading(false)
        }
      }
    }

    void loadProfile()
    return () => {
      active = false
    }
  }, [])

  useEffect(() => {
    if (window.location.hash === '#password') {
      passwordSectionRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' })
    }
  }, [loading])

  const initials = useMemo(() => {
    if (!profile) {
      return 'S'
    }

    return `${profile.firstName.charAt(0)}${profile.lastName.charAt(0)}`.toLocaleUpperCase(
      'tr-TR',
    )
  }, [profile])

  async function handleProfileSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSavingProfile(true)
    setProfileError('')

    try {
      const response = await updateMyProfile({
        firstName: firstName.trim(),
        lastName: lastName.trim(),
      })
      setProfile(response)
      await refreshUser()
      setSnackbarMessage('Profil bilgileri güncellendi.')
    } catch (error: unknown) {
      setProfileError(
        getFriendlyApiErrorMessage(error, {
          fallback: 'Profil bilgileri güncellenemedi. Lütfen tekrar deneyin.',
          unavailableMessage:
            'SupportFlow hizmetine ulaşılamıyor. Lütfen daha sonra tekrar deneyin.',
        }),
      )
    } finally {
      setSavingProfile(false)
    }
  }

  async function handlePasswordSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setChangingPassword(true)
    setPasswordError('')

    try {
      await changeMyPassword({
        currentPassword,
        newPassword,
        confirmNewPassword,
      })
      setCurrentPassword('')
      setNewPassword('')
      setConfirmNewPassword('')
      setSnackbarMessage('Şifreniz başarıyla değiştirildi.')
    } catch (error: unknown) {
      setPasswordError(
        getFriendlyApiErrorMessage(error, {
          fallback: 'Şifre değiştirilemedi. Lütfen tekrar deneyin.',
          unavailableMessage:
            'SupportFlow hizmetine ulaşılamıyor. Lütfen daha sonra tekrar deneyin.',
        }),
      )
    } finally {
      setChangingPassword(false)
    }
  }

  if (loading) {
    return (
      <Box sx={{ display: 'grid', minHeight: 320, placeItems: 'center' }}>
        <CircularProgress aria-label="Profil yükleniyor" />
      </Box>
    )
  }

  if (loadError || !profile) {
    return <Alert severity="error">{loadError || 'Profil bulunamadı.'}</Alert>
  }

  return (
    <>
      <Stack spacing={3}>
        <PageHeader
          description="Hesap bilgilerinizi güncelleyin ve erişim güvenliğini yönetin."
          eyebrow="Hesap"
          title="Profilim"
        />

        <Box
          sx={{
            display: 'grid',
            gap: 2.5,
            gridTemplateColumns: { xs: '1fr', xl: 'minmax(320px, 0.88fr) minmax(0, 1.5fr)' },
          }}
        >
          <Paper sx={{ border: '1px solid', borderColor: 'divider', p: 3 }}>
            <Stack spacing={2.25} sx={{ alignItems: 'flex-start' }}>
              <SupportFlowLogo width={156} />
              <Avatar
                sx={{
                  bgcolor: 'primary.main',
                  boxShadow: '0 18px 36px rgba(124, 58, 237, 0.22)',
                  fontSize: 28,
                  fontWeight: 800,
                  height: 72,
                  width: 72,
                }}
              >
                {initials}
              </Avatar>
              <Box>
                <Typography sx={{ fontWeight: 800 }} variant="h5">
                  {profile.firstName} {profile.lastName}
                </Typography>
                <Typography color="text.secondary" sx={{ mt: 0.4 }} variant="body2">
                  {profile.email}
                </Typography>
              </Box>
              <Stack spacing={1} sx={{ width: '100%' }}>
                <Typography variant="body2">
                  <strong>Rol:</strong> {getUserRoleLabel(profile.role)}
                </Typography>
                <Typography variant="body2">
                  <strong>Hesap durumu:</strong> {profile.isActive ? 'Aktif' : 'Pasif'}
                </Typography>
                <Typography variant="body2">
                  <strong>Şirket:</strong> {profile.companyId ?? 'Tanımlı değil'}
                </Typography>
                <Typography variant="body2">
                  <strong>Oluşturulma:</strong> {formatDate(profile.createdAt)}
                </Typography>
              </Stack>
            </Stack>
          </Paper>

          <Stack spacing={2.5}>
            <Paper component="form" noValidate onSubmit={handleProfileSubmit} sx={{ border: '1px solid', borderColor: 'divider', p: 3 }}>
              <Stack spacing={2.25}>
                <Box>
                  <Typography sx={{ fontWeight: 700 }} variant="h6">
                    Profil Bilgileri
                  </Typography>
                  <Typography color="text.secondary" sx={{ mt: 0.6 }} variant="body2">
                    Ad ve soyad bilgilerinizi güncelleyebilirsiniz. E-posta bu aşamada salt okunurdur.
                  </Typography>
                </Box>
                {profileError && <Alert severity="error">{profileError}</Alert>}
                <Stack direction={{ xs: 'column', md: 'row' }} spacing={2}>
                  <TextField label="Ad" onChange={(event) => setFirstName(event.target.value)} value={firstName} />
                  <TextField label="Soyad" onChange={(event) => setLastName(event.target.value)} value={lastName} />
                </Stack>
                <TextField
                  label="E-posta"
                  slotProps={{ input: { readOnly: true } }}
                  value={profile.email}
                />
                <Box sx={{ display: 'flex', justifyContent: 'flex-end' }}>
                  <Button disabled={savingProfile} type="submit" variant="contained">
                    {savingProfile ? 'Kaydediliyor...' : 'Bilgileri Kaydet'}
                  </Button>
                </Box>
              </Stack>
            </Paper>

            <Paper sx={{ border: '1px solid', borderColor: 'divider', p: 3 }}>
              <Stack spacing={1.1}>
                <Typography sx={{ fontWeight: 700 }} variant="h6">
                  Hesap Bilgileri
                </Typography>
                <Typography color="text.secondary" variant="body2">
                  Gmail korelasyonu ve rol güvenliği nedeniyle e-posta, rol ve şirket alanları bu fazda düzenlenmez.
                </Typography>
              </Stack>
            </Paper>

            <Paper
              component="form"
              id="password"
              noValidate
              onSubmit={handlePasswordSubmit}
              ref={passwordSectionRef}
              sx={{ border: '1px solid', borderColor: 'divider', p: 3 }}
            >
              <Stack spacing={2.25}>
                <Box>
                  <Typography sx={{ fontWeight: 700 }} variant="h6">
                    Şifre Değiştir
                  </Typography>
                  <Typography color="text.secondary" sx={{ mt: 0.6 }} variant="body2">
                    Güvenliğiniz için mevcut şifrenizi doğrulayarak yeni şifrenizi belirleyin.
                  </Typography>
                </Box>
                {passwordError && <Alert severity="error">{passwordError}</Alert>}
                <TextField
                  label="Mevcut Şifre"
                  slotProps={{
                    input: {
                      endAdornment: (
                        <InputAdornment position="end">
                          <IconButton
                            aria-label={showCurrentPassword ? 'Şifreyi gizle' : 'Şifreyi göster'}
                            edge="end"
                            onClick={() => setShowCurrentPassword((value) => !value)}
                            type="button"
                          >
                            {showCurrentPassword ? <VisibilityOff /> : <Visibility />}
                          </IconButton>
                        </InputAdornment>
                      ),
                    },
                  }}
                  type={showCurrentPassword ? 'text' : 'password'}
                  value={currentPassword}
                  onChange={(event) => setCurrentPassword(event.target.value)}
                />
                <TextField
                  helperText="En az 8 karakter olmalıdır."
                  label="Yeni Şifre"
                  slotProps={{
                    input: {
                      endAdornment: (
                        <InputAdornment position="end">
                          <IconButton
                            aria-label={showNewPassword ? 'Şifreyi gizle' : 'Şifreyi göster'}
                            edge="end"
                            onClick={() => setShowNewPassword((value) => !value)}
                            type="button"
                          >
                            {showNewPassword ? <VisibilityOff /> : <Visibility />}
                          </IconButton>
                        </InputAdornment>
                      ),
                    },
                  }}
                  type={showNewPassword ? 'text' : 'password'}
                  value={newPassword}
                  onChange={(event) => setNewPassword(event.target.value)}
                />
                <TextField
                  label="Yeni Şifre Tekrarı"
                  slotProps={{
                    input: {
                      endAdornment: (
                        <InputAdornment position="end">
                          <IconButton
                            aria-label={showConfirmNewPassword ? 'Şifreyi gizle' : 'Şifreyi göster'}
                            edge="end"
                            onClick={() => setShowConfirmNewPassword((value) => !value)}
                            type="button"
                          >
                            {showConfirmNewPassword ? <VisibilityOff /> : <Visibility />}
                          </IconButton>
                        </InputAdornment>
                      ),
                    },
                  }}
                  type={showConfirmNewPassword ? 'text' : 'password'}
                  value={confirmNewPassword}
                  onChange={(event) => setConfirmNewPassword(event.target.value)}
                />
                <Box sx={{ display: 'flex', justifyContent: 'flex-end' }}>
                  <Button
                    disabled={changingPassword}
                    startIcon={<LockOutlined />}
                    type="submit"
                    variant="contained"
                  >
                    {changingPassword ? 'Şifre Güncelleniyor...' : 'Şifreyi Güncelle'}
                  </Button>
                </Box>
              </Stack>
            </Paper>
          </Stack>
        </Box>
      </Stack>

      <Snackbar
        autoHideDuration={3500}
        onClose={() => setSnackbarMessage('')}
        open={Boolean(snackbarMessage)}
      >
        <Alert onClose={() => setSnackbarMessage('')} severity="success" variant="filled">
          {snackbarMessage}
        </Alert>
      </Snackbar>
    </>
  )
}
