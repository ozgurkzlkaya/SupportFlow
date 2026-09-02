import { ArrowBackOutlined, PersonOffOutlined, PersonOutlined } from '@mui/icons-material'
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Paper,
  Snackbar,
  Stack,
  Typography,
} from '@mui/material'
import axios from 'axios'
import { useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { PageHeader } from '../../components/common/PageHeader'
import { PaginationControls } from '../../components/common/PaginationControls'
import { UserAccountStatusChip } from '../../components/common/UserAccountStatusChip'
import { UserRoleChip } from '../../components/common/UserRoleChip'
import { useAuth } from '../../hooks/useAuth'
import { getAdminUserAudits, getAdminUserDetail, updateAdminUserStatus } from '../../services/adminUserService'
import { UserAdminAuditAction, type AdminUserAudit, type AdminUserDetail } from '../../types/adminUser'
import { UserRole } from '../../types/auth'
import type { PaginationMetadata } from '../../types/pagination'
import { formatDate } from '../../utils/formatDate'
import { getUserRoleLabel } from '../../utils/userLabels'

interface DetailValueProps {
  label: string
  value: string | number
}

function DetailValue({ label, value }: DetailValueProps) {
  return (
    <Box>
      <Typography color="text.secondary" variant="caption">{label}</Typography>
      <Typography sx={{ fontWeight: 700, mt: 0.35 }} variant="body2">{value}</Typography>
    </Box>
  )
}

function getStatusErrorMessage(error: unknown, isActivating: boolean): string {
  if (axios.isAxiosError(error) && error.response?.status === 400 && !isActivating) {
    return 'Son aktif yönetici hesabı pasifleştirilemez.'
  }

  if (axios.isAxiosError(error) && error.response?.status === 404) {
    return 'Kullanıcı bulunamadı veya artık erişilebilir değil.'
  }

  return 'Hesap durumu güncellenemedi. Lütfen tekrar deneyin.'
}

function getStatusDescription(user: AdminUserDetail, targetIsActive: boolean): string {
  if (targetIsActive) {
    return `${user.fullName} hesabı yeniden etkinleştirilecek.`
  }

  switch (user.role) {
    case UserRole.TechnicalStaff:
      return 'Bu personel pasifleştirildiğinde yeni taleplere atanamaz. Mevcut aktif talep atamaları korunur.'
    case UserRole.Customer:
      return 'Bu müşteri pasifleştirildiğinde giriş yapamaz ancak geçmiş talepleri ve mesajları korunur.'
    case UserRole.Admin:
      return 'Bu yönetici pasifleştirildiğinde sisteme erişemez.'
  }
}

function getAuditActionLabel(action: UserAdminAuditAction): string {
  return action === UserAdminAuditAction.Activated ? 'Aktifleştirildi' : 'Pasifleştirildi'
}

const emptyAuditPagination: PaginationMetadata = {
  page: 1,
  pageSize: 25,
  totalCount: 0,
  totalPages: 0,
  hasPreviousPage: false,
  hasNextPage: false,
}

export function AdminUserDetailPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const { user: currentUser } = useAuth()
  const userId = Number(id)
  const [user, setUser] = useState<AdminUserDetail | null>(null)
  const [loading, setLoading] = useState(true)
  const [notFound, setNotFound] = useState(false)
  const [loadError, setLoadError] = useState('')
  const [statusError, setStatusError] = useState('')
  const [successMessage, setSuccessMessage] = useState('')
  const [pendingStatus, setPendingStatus] = useState<boolean | null>(null)
  const [updating, setUpdating] = useState(false)
  const [reloadKey, setReloadKey] = useState(0)
  const [auditItems, setAuditItems] = useState<AdminUserAudit[]>([])
  const [auditPagination, setAuditPagination] = useState<PaginationMetadata>(emptyAuditPagination)
  const [auditLoading, setAuditLoading] = useState(true)
  const [auditError, setAuditError] = useState('')

  useEffect(() => {
    if (!Number.isInteger(userId) || userId <= 0) {
      setLoading(false)
      setNotFound(true)
      return
    }

    let active = true
    async function loadUser() {
      setLoading(true)
      setLoadError('')
      setNotFound(false)

      try {
        const response = await getAdminUserDetail(userId)
        if (active) setUser(response)
      } catch (error: unknown) {
        if (!active) return
        if (axios.isAxiosError(error) && error.response?.status === 404) {
          setNotFound(true)
          setUser(null)
        } else {
          setLoadError('Kullanıcı detayları yüklenemedi. Lütfen tekrar deneyin.')
        }
      } finally {
        if (active) setLoading(false)
      }
    }

    void loadUser()
    return () => { active = false }
  }, [reloadKey, userId])

  useEffect(() => {
    if (!Number.isInteger(userId) || userId <= 0) return

    let active = true
    async function loadAudits() {
      setAuditLoading(true)
      setAuditError('')
      try {
        const response = await getAdminUserAudits(
          userId,
          auditPagination.page,
          auditPagination.pageSize,
        )
        if (active) {
          setAuditItems(response.items)
          setAuditPagination(response.pagination)
        }
      } catch {
        if (active) setAuditError('Yönetim geçmişi yüklenemedi. Lütfen tekrar deneyin.')
      } finally {
        if (active) setAuditLoading(false)
      }
    }

    void loadAudits()
    return () => { active = false }
  }, [reloadKey, userId, auditPagination.page, auditPagination.pageSize])

  const isCurrentUser = currentUser?.id === user?.id
  const targetIsActive = pendingStatus ?? user?.isActive ?? false
  const isDeactivation = pendingStatus === false

  async function handleStatusUpdate() {
    if (!user || pendingStatus === null) return

    setUpdating(true)
    setStatusError('')
    try {
      await updateAdminUserStatus(user.id, pendingStatus)
      setPendingStatus(null)
      setSuccessMessage(pendingStatus ? 'Kullanıcı hesabı etkinleştirildi.' : 'Kullanıcı hesabı pasifleştirildi.')
      setAuditPagination((current) => ({ ...current, page: 1 }))
      setReloadKey((value) => value + 1)
    } catch (error: unknown) {
      setStatusError(getStatusErrorMessage(error, pendingStatus))
    } finally {
      setUpdating(false)
    }
  }

  if (loading) {
    return <Box sx={{ display: 'grid', minHeight: 320, placeItems: 'center' }}><CircularProgress aria-label="Kullanıcı detayları yükleniyor" /></Box>
  }

  if (notFound) {
    return (
      <Stack spacing={2.5}>
        <Button onClick={() => navigate('/admin/users')} startIcon={<ArrowBackOutlined />} sx={{ alignSelf: 'flex-start' }} variant="text">Kullanıcılara Dön</Button>
        <Alert severity="info">İstenen kullanıcı bulunamadı.</Alert>
      </Stack>
    )
  }

  if (loadError || !user) {
    return <Alert action={<Button color="inherit" onClick={() => setReloadKey((value) => value + 1)}>Tekrar Dene</Button>} severity="error">{loadError || 'Kullanıcı detayları yüklenemedi.'}</Alert>
  }

  return (
    <>
      <Stack spacing={3}>
        <PageHeader
          actions={
            <Stack spacing={0.75} sx={{ alignItems: { xs: 'stretch', md: 'flex-end' } }}>
              {isCurrentUser && user.isActive ? (
                <>
                  <Button disabled startIcon={<PersonOffOutlined />} variant="outlined">Pasifleştir</Button>
                  <Typography color="text.secondary" variant="caption">Kendi hesabınızı pasifleştiremezsiniz.</Typography>
                </>
              ) : (
                <Button
                  color={user.isActive ? 'warning' : 'primary'}
                  onClick={() => { setStatusError(''); setPendingStatus(!user.isActive) }}
                  startIcon={user.isActive ? <PersonOffOutlined /> : <PersonOutlined />}
                  variant={user.isActive ? 'outlined' : 'contained'}
                >
                  {user.isActive ? 'Pasifleştir' : 'Aktifleştir'}
                </Button>
              )}
              <Button onClick={() => navigate('/admin/users')} startIcon={<ArrowBackOutlined />} variant="text">Kullanıcılara Dön</Button>
            </Stack>
          }
          description="Hesap bilgilerini, güvenli erişim durumunu ve role bağlı özetleri görüntüleyin."
          eyebrow="Yönetim"
          title="Kullanıcı Detayı"
        />

        {statusError ? <Alert severity="error">{statusError}</Alert> : null}

        <Box sx={{ display: 'grid', gap: 2.5, gridTemplateColumns: { xs: '1fr', xl: 'minmax(0, 1.3fr) minmax(300px, 0.7fr)' } }}>
          <Paper sx={{ border: '1px solid', borderColor: 'divider', p: { xs: 2.25, sm: 3 } }}>
            <Stack spacing={2.25}>
              <Box>
                <Typography sx={{ fontWeight: 800 }} variant="h5">{user.fullName}</Typography>
                <Typography color="text.secondary" sx={{ mt: 0.45 }} variant="body2">{user.email}</Typography>
              </Box>
              <Stack direction="row" spacing={0.8} sx={{ flexWrap: 'wrap' }}>
                <UserRoleChip role={user.role} />
                <UserAccountStatusChip isActive={user.isActive} />
              </Stack>
              <Box sx={{ display: 'grid', gap: 2, gridTemplateColumns: { xs: '1fr', sm: 'repeat(2, minmax(0, 1fr))' } }}>
                <DetailValue label="Ad Soyad" value={user.fullName} />
                <DetailValue label="E-posta" value={user.email} />
                <DetailValue label="Rol" value={getUserRoleLabel(user.role)} />
                <DetailValue label="Durum" value={user.isActive ? 'Aktif' : 'Pasif'} />
                <DetailValue label="Oluşturulma" value={formatDate(user.createdAt)} />
                <DetailValue label="Son Güncelleme" value={formatDate(user.updatedAt)} />
              </Box>
            </Stack>
          </Paper>

          {user.technicalStaffContext ? (
            <Paper sx={{ border: '1px solid', borderColor: 'divider', p: { xs: 2.25, sm: 3 } }}>
              <Stack spacing={2}>
                <Box>
                  <Typography sx={{ fontWeight: 800 }} variant="h6">Operasyon Özeti</Typography>
                  <Typography color="text.secondary" sx={{ mt: 0.45 }} variant="body2">Bu personele atanmış aktif taleplerin kompakt görünümü.</Typography>
                </Box>
                <Box sx={{ display: 'grid', gap: 1.25, gridTemplateColumns: 'repeat(2, minmax(0, 1fr))' }}>
                  <DetailValue label="Aktif Talep" value={user.technicalStaffContext.activeTicketCount} />
                  <DetailValue label="Kritik" value={user.technicalStaffContext.criticalTicketCount} />
                  <DetailValue label="Yanıt Bekleyen" value={user.technicalStaffContext.awaitingReplyCount} />
                  <DetailValue label="SLA İhlali" value={user.technicalStaffContext.slaBreachedCount} />
                </Box>
              </Stack>
            </Paper>
          ) : null}

          {user.customerContext ? (
            <Paper sx={{ border: '1px solid', borderColor: 'divider', p: { xs: 2.25, sm: 3 } }}>
              <Stack spacing={2}>
                <Box>
                  <Typography sx={{ fontWeight: 800 }} variant="h6">Talep Özeti</Typography>
                  <Typography color="text.secondary" sx={{ mt: 0.45 }} variant="body2">Bu kayıtlı müşterinin SupportFlow taleplerinin özeti.</Typography>
                </Box>
                <Box sx={{ display: 'grid', gap: 1.25, gridTemplateColumns: 'repeat(2, minmax(0, 1fr))' }}>
                  <DetailValue label="Toplam Talep" value={user.customerContext.totalTicketCount} />
                  <DetailValue label="Aktif" value={user.customerContext.activeTicketCount} />
                  <DetailValue label="Çözüldü" value={user.customerContext.resolvedTicketCount} />
                  <DetailValue label="Kapandı" value={user.customerContext.closedTicketCount} />
                </Box>
              </Stack>
            </Paper>
          ) : null}
        </Box>

        <Paper sx={{ border: '1px solid', borderColor: 'divider', p: { xs: 2.25, sm: 3 } }}>
          <Stack spacing={2}>
            <Box>
              <Typography sx={{ fontWeight: 800 }} variant="h6">Yönetim Geçmişi</Typography>
              <Typography color="text.secondary" sx={{ mt: 0.45 }} variant="body2">
                Bu hesap için gerçekleştirilen etkinlik durumu değişiklikleri.
              </Typography>
            </Box>

            {auditLoading ? (
              <Box sx={{ display: 'grid', minHeight: 96, placeItems: 'center' }}>
                <CircularProgress aria-label="Yönetim geçmişi yükleniyor" size={28} />
              </Box>
            ) : auditError ? (
              <Alert severity="error">{auditError}</Alert>
            ) : auditItems.length === 0 ? (
              <Typography color="text.secondary" variant="body2">Yönetim işlemi bulunmuyor.</Typography>
            ) : (
              <Stack divider={<Box sx={{ borderTop: '1px solid', borderColor: 'divider' }} />} spacing={0}>
                {auditItems.map((audit) => (
                  <Stack
                    direction={{ xs: 'column', sm: 'row' }}
                    key={audit.id}
                    spacing={1.25}
                    sx={{ justifyContent: 'space-between', py: 1.5 }}
                  >
                    <Box>
                      <Typography sx={{ fontWeight: 700 }} variant="body2">{getAuditActionLabel(audit.action)}</Typography>
                      <Typography color="text.secondary" sx={{ mt: 0.35 }} variant="caption">
                        {audit.actorDisplayName} · Durum: {audit.oldIsActive ? 'Aktif' : 'Pasif'} → {audit.newIsActive ? 'Aktif' : 'Pasif'}
                      </Typography>
                    </Box>
                    <Typography color="text.secondary" variant="caption">{formatDate(audit.occurredAt)}</Typography>
                  </Stack>
                ))}
              </Stack>
            )}

            {auditPagination.totalCount > 0 ? (
              <PaginationControls
                itemLabel="işlem"
                onPageChange={(page) => setAuditPagination((current) => ({ ...current, page }))}
                onPageSizeChange={(pageSize) => setAuditPagination((current) => ({ ...current, page: 1, pageSize }))}
                pagination={auditPagination}
              />
            ) : null}
          </Stack>
        </Paper>
      </Stack>

      <Dialog aria-describedby="user-status-confirmation-description" onClose={updating ? undefined : () => setPendingStatus(null)} open={pendingStatus !== null}>
        <DialogTitle>{targetIsActive ? 'Kullanıcıyı Etkinleştir' : 'Kullanıcıyı Pasifleştir'}</DialogTitle>
        <DialogContent dividers>
          <Stack spacing={1.5}>
            <Typography id="user-status-confirmation-description"><strong>{user.fullName}</strong> · {getUserRoleLabel(user.role)}</Typography>
            <Typography color="text.secondary" variant="body2">{getStatusDescription(user, targetIsActive)}</Typography>
            {isDeactivation && user.role === UserRole.TechnicalStaff && user.technicalStaffContext?.activeTicketCount ? (
              <Alert severity="warning">Bu personelin {user.technicalStaffContext.activeTicketCount} aktif talep ataması korunacaktır.</Alert>
            ) : null}
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button disabled={updating} onClick={() => setPendingStatus(null)}>Vazgeç</Button>
          <Button color={targetIsActive ? 'primary' : 'warning'} disabled={updating} onClick={() => void handleStatusUpdate()} variant="contained">
            {updating ? 'Kaydediliyor...' : targetIsActive ? 'Aktifleştir' : 'Pasifleştir'}
          </Button>
        </DialogActions>
      </Dialog>

      <Snackbar autoHideDuration={4000} onClose={() => setSuccessMessage('')} open={Boolean(successMessage)}>
        <Alert onClose={() => setSuccessMessage('')} severity="success" variant="filled">{successMessage}</Alert>
      </Snackbar>
    </>
  )
}
