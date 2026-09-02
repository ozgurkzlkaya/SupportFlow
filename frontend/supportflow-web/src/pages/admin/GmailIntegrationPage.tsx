import {
  CheckCircleOutlined,
  MailOutlined,
  Refresh,
  SendOutlined,
} from '@mui/icons-material'
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Paper,
  Stack,
  Typography,
} from '@mui/material'
import { useEffect, useMemo, useState } from 'react'
import { EmptyStateCard } from '../../components/common/EmptyStateCard'
import { MetricCard } from '../../components/common/MetricCard'
import { PageHeader } from '../../components/common/PageHeader'
import {
  getGmailOAuthStatus,
  getGmailMessageDiagnostics,
  processGmailMailbox,
  startGmailOAuth,
} from '../../services/gmailService'
import type {
  GmailMailboxProcessingSummary,
  GmailMessageDiagnostic,
  GmailOAuthStatus,
} from '../../types/gmail'
import { getFriendlyApiErrorMessage } from '../../utils/getApiErrorMessage'
import { formatDate } from '../../utils/formatDate'
import { getUserRoleLabel } from '../../utils/userLabels'

function CapabilityItem({
  label,
  active,
}: {
  label: string
  active: boolean
}) {
  return (
    <Paper
      sx={{
        background: active
          ? 'linear-gradient(180deg, rgba(245, 243, 255, 0.96), rgba(255,255,255,0.96))'
          : 'transparent',
        border: '1px solid',
        borderColor: active ? 'primary.light' : 'divider',
        p: 1.5,
      }}
    >
      <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
        <CheckCircleOutlined color={active ? 'success' : 'disabled'} fontSize="small" />
        <Box>
          <Typography sx={{ fontWeight: 700 }} variant="body2">
            {label}
          </Typography>
          <Typography color="text.secondary" variant="caption">
            {active ? 'Hazır' : 'Eksik'}
          </Typography>
        </Box>
      </Stack>
    </Paper>
  )
}

type GmailSummaryMetricKey = Exclude<keyof GmailMailboxProcessingSummary, 'results'>

const summaryLabels: Array<{
  key: GmailSummaryMetricKey
  label: string
  accent: string
}> = [
  { key: 'processed', label: 'İncelenen', accent: '#7c3aed' },
  { key: 'newTickets', label: 'Yeni Talep', accent: '#5b21b6' },
  { key: 'newExternalTickets', label: 'Yeni Harici Talep', accent: '#a16207' },
  { key: 'externalRequestersCreated', label: 'Harici Müşteri Oluşturuldu', accent: '#b45309' },
  { key: 'externalRequestersMatched', label: 'Harici Müşteri Eşleşti', accent: '#92400e' },
  { key: 'messagesAdded', label: 'Eklenen Mesaj', accent: '#4338ca' },
  { key: 'alreadyProcessed', label: 'Zaten İşlenmiş', accent: '#475569' },
  { key: 'unknownCustomers', label: 'Bilinmeyen Müşteri', accent: '#c2410c' },
  { key: 'senderMismatches', label: 'Gönderen Uyuşmazlığı', accent: '#b91c1c' },
  { key: 'selfMessagesIgnored', label: 'Kendi Mesajı', accent: '#1d4ed8' },
  { key: 'systemMessagesSkipped', label: 'Sistem Maili Atlandı', accent: '#64748b' },
  { key: 'failed', label: 'Başarısız', accent: '#991b1b' },
]

export function GmailIntegrationPage() {
  const [status, setStatus] = useState<GmailOAuthStatus | null>(null)
  const [summary, setSummary] = useState<GmailMailboxProcessingSummary | null>(null)
  const [loading, setLoading] = useState(true)
  const [refreshing, setRefreshing] = useState(false)
  const [authorizing, setAuthorizing] = useState(false)
  const [processing, setProcessing] = useState(false)
  const [error, setError] = useState('')
  const [processError, setProcessError] = useState('')
  const [processSuccess, setProcessSuccess] = useState('')
  const [diagnostics, setDiagnostics] = useState<GmailMessageDiagnostic[] | null>(null)
  const [diagnosticsLoading, setDiagnosticsLoading] = useState(false)
  const [diagnosticsError, setDiagnosticsError] = useState('')

  async function loadStatus(showRefreshState = false) {
    if (showRefreshState) {
      setRefreshing(true)
    } else {
      setLoading(true)
    }

    setError('')

    try {
      const response = await getGmailOAuthStatus()
      setStatus(response)
    } catch (error: unknown) {
      setError(
        getFriendlyApiErrorMessage(error, {
          fallback: 'Gmail entegrasyon durumu alınamadı. Lütfen tekrar deneyin.',
          forbiddenMessage: 'Bu işlem için yetkiniz bulunmuyor.',
          unavailableMessage:
            'SupportFlow hizmetine ulaşılamıyor. Lütfen daha sonra tekrar deneyin.',
        }),
      )
    } finally {
      setLoading(false)
      setRefreshing(false)
    }
  }

  useEffect(() => {
    void loadStatus()
  }, [])

  const isHealthy = Boolean(
    status?.configured && status.authorized && status.canRead && status.canSend,
  )
  const needsAuthorization = useMemo(
    () => !status?.authorized || !status.canRead || !status.canSend,
    [status],
  )
  const backgroundSync = status?.backgroundSync
  const isRateLimited = Boolean(backgroundSync?.isRateLimited)
  const rateLimitedUntil = backgroundSync?.rateLimitedUntil ?? null

  async function handleAuthorize() {
    setAuthorizing(true)
    setError('')

    try {
      const response = await startGmailOAuth()
      const openedWindow = window.open(response.authorizationUrl, '_blank', 'noopener,noreferrer')

      if (!openedWindow) {
        window.location.href = response.authorizationUrl
      }
    } catch (error: unknown) {
      setError(
        getFriendlyApiErrorMessage(error, {
          fallback: 'Google yetkilendirme bağlantısı oluşturulamadı.',
          forbiddenMessage: 'Bu işlem için yetkiniz bulunmuyor.',
          unavailableMessage:
            'SupportFlow hizmetine ulaşılamıyor. Lütfen daha sonra tekrar deneyin.',
        }),
      )
    } finally {
      setAuthorizing(false)
    }
  }

  async function handleProcessMailbox() {
    if (isRateLimited) {
      setProcessError('Gmail API geçici olarak hız sınırında. Yeniden deneme zamanını kontrol edin.')
      return
    }

    setProcessing(true)
    setProcessError('')
    setProcessSuccess('')
    setSummary(null)

    try {
      const response = await processGmailMailbox()
      setSummary(response)

      if (response.failed > 0 || response.unknownCustomers > 0 || response.senderMismatches > 0) {
        setProcessError('İşlem tamamlandı, bazı iletiler işlenemedi.')
      } else {
        setProcessSuccess('Posta kutusu başarıyla işlendi.')
      }
    } catch (error: unknown) {
      setProcessError(
        getFriendlyApiErrorMessage(error, {
          fallback: 'Gelen mailler işlenemedi. Lütfen tekrar deneyin.',
          forbiddenMessage: 'Bu işlem için yetkiniz bulunmuyor.',
          unavailableMessage:
            'SupportFlow hizmetine ulaşılamıyor. Lütfen daha sonra tekrar deneyin.',
        }),
      )
      void loadStatus()
    } finally {
      setProcessing(false)
    }
  }

  async function handleLoadDiagnostics() {
    if (isRateLimited) {
      setDiagnosticsError('Gmail API geçici olarak hız sınırında. Tanılama isteği gönderilmedi.')
      return
    }

    setDiagnosticsLoading(true)
    setDiagnosticsError('')

    try {
      setDiagnostics(await getGmailMessageDiagnostics())
    } catch (error: unknown) {
      setDiagnosticsError(
        getFriendlyApiErrorMessage(error, {
          fallback: 'Gmail tanılama bilgileri alınamadı.',
          forbiddenMessage: 'Bu işlem için yetkiniz bulunmuyor.',
          unavailableMessage: 'SupportFlow hizmetine ulaşılamıyor. Lütfen daha sonra tekrar deneyin.',
        }),
      )
      void loadStatus()
    } finally {
      setDiagnosticsLoading(false)
    }
  }

  return (
    <Stack spacing={3}>
      <PageHeader
        actions={
          <Button
            disabled={loading || refreshing}
            onClick={() => void loadStatus(true)}
            startIcon={
              refreshing ? (
                <CircularProgress aria-hidden="true" color="inherit" size={18} />
              ) : (
                <Refresh />
              )
            }
            variant="outlined"
          >
            {refreshing ? 'Yenileniyor...' : 'Durumu Yenile'}
          </Button>
        }
        description="OAuth yetkilerini doğrulayın ve Gmail gelen kutusundaki destek iletilerini SupportFlow ile eşitleyin."
        eyebrow="Gmail Yönetimi"
        title="Gmail Entegrasyonu"
      />

      {loading ? (
        <Box sx={{ display: 'grid', minHeight: 320, placeItems: 'center' }}>
          <CircularProgress aria-label="Gmail durumu yükleniyor" />
        </Box>
      ) : error && !status ? (
        <Alert severity="error">{error}</Alert>
      ) : (
        <>
          {error && <Alert severity="error">{error}</Alert>}

          <Paper sx={{ border: '1px solid', borderColor: 'divider', p: 3 }}>
            <Stack spacing={2.5}>
              <Stack
                direction={{ xs: 'column', md: 'row' }}
                spacing={1.5}
                sx={{ alignItems: { md: 'center' }, justifyContent: 'space-between' }}
              >
                <Box>
                  <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
                    <CheckCircleOutlined color={isHealthy ? 'success' : 'disabled'} />
                    <Typography sx={{ fontWeight: 700 }} variant="h6">
                      {isHealthy ? 'Entegrasyon Sağlıklı' : 'Sorun Var'}
                    </Typography>
                  </Stack>
                  <Typography color="text.secondary" sx={{ mt: 0.6 }} variant="body2">
                    Mailbox: {status?.mailbox || 'Yok'}
                  </Typography>
                </Box>
              </Stack>

              <Box
                sx={{
                  display: 'grid',
                  gap: 1.5,
                  gridTemplateColumns: { xs: '1fr', sm: 'repeat(2, minmax(0, 1fr))' },
                }}
              >
                <CapabilityItem active={Boolean(status?.configured)} label="Yapılandırma" />
                <CapabilityItem active={Boolean(status?.authorized)} label="Yetkilendirme" />
                <CapabilityItem active={Boolean(status?.canRead)} label="Gelen Mail Okuma" />
                <CapabilityItem active={Boolean(status?.canSend)} label="Mail Gönderme" />
              </Box>
            </Stack>
          </Paper>

          <Paper sx={{ border: '1px solid', borderColor: 'divider', p: 3 }}>
            <Stack spacing={1.25}>
              <Stack
                direction={{ xs: 'column', sm: 'row' }}
                spacing={1}
                sx={{ alignItems: { sm: 'center' }, justifyContent: 'space-between' }}
              >
                <Box>
                  <Typography sx={{ fontWeight: 700 }} variant="h6">
                    Otomatik Senkronizasyon
                  </Typography>
                  <Typography color="text.secondary" sx={{ mt: 0.4 }} variant="body2">
                    {backgroundSync?.enabled
                      ? `Kontrol aralığı: ${backgroundSync.intervalSeconds} sn`
                      : 'Otomatik senkronizasyon kapalı.'}
                  </Typography>
                </Box>
                <Chip
                  color={isRateLimited ? 'warning' : backgroundSync?.enabled ? 'success' : 'default'}
                  label={isRateLimited ? 'Hız sınırı bekleniyor' : backgroundSync?.enabled ? 'Aktif' : 'Kapalı'}
                  size="small"
                  variant={isRateLimited || backgroundSync?.enabled ? 'filled' : 'outlined'}
                />
              </Stack>

              {isRateLimited && (
                <Alert severity="warning">
                  Gmail API geçici olarak hız sınırına ulaştı. Yeniden deneme:{' '}
                  {formatDate(rateLimitedUntil)}
                </Alert>
              )}

              {backgroundSync?.enabled ? (
                <Stack spacing={0.35}>
                  <Typography color="text.secondary" variant="caption">
                    Durum: {isRateLimited
                      ? 'Hız sınırı bekleniyor'
                      : backgroundSync.isRunning
                        ? 'İşleniyor'
                        : 'Beklemede'}
                  </Typography>
                  <Typography color="text.secondary" variant="caption">
                    Son başarılı kontrol: {formatDate(backgroundSync.lastSuccessfulAt)}
                  </Typography>
                  {backgroundSync.lastSummary ? (
                    <Typography color="text.secondary" variant="caption">
                      Son sonuç: {backgroundSync.lastSummary.examined} incelendi, {backgroundSync.lastSummary.newTickets} yeni talep, {backgroundSync.lastSummary.messagesAdded} mesaj eklendi.
                    </Typography>
                  ) : null}
                  {backgroundSync.lastError ? (
                    <Alert severity="warning" sx={{ mt: 0.75 }}>
                      {backgroundSync.lastError}
                    </Alert>
                  ) : null}
                </Stack>
              ) : null}
            </Stack>
          </Paper>

          <Paper sx={{ border: '1px solid', borderColor: 'divider', p: 3 }}>
            <Stack spacing={2}>
              <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5} sx={{ justifyContent: 'space-between' }}>
                <Box>
                  <Typography sx={{ fontWeight: 700 }} variant="h6">
                    Gmail Tanılama
                  </Typography>
                  <Typography color="text.secondary" sx={{ mt: 0.6 }} variant="body2">
                    Son iletilerin gönderici, zaman ve eşleşme metadatasını salt okunur olarak inceleyin.
                  </Typography>
                </Box>
                <Button disabled={diagnosticsLoading || isRateLimited} onClick={() => void handleLoadDiagnostics()} variant="outlined">
                  {diagnosticsLoading ? 'İnceleniyor...' : 'Son Mesajları İncele'}
                </Button>
              </Stack>

              {diagnosticsError && <Alert severity="error">{diagnosticsError}</Alert>}

              {diagnostics && (
                <Stack spacing={1}>
                  {diagnostics.map((message) => (
                    <Paper key={message.gmailMessageId} sx={{ bgcolor: 'action.hover', p: 1.5 }} variant="outlined">
                      <Typography sx={{ fontWeight: 700 }} variant="body2">
                        {message.subject}
                      </Typography>
                      <Typography color="text.secondary" variant="caption">
                        {message.parsedSenderEmail || message.rawFrom} · {message.internalDateEuropeIstanbul}
                      </Typography>
                      <Typography color="text.secondary" sx={{ display: 'block' }} variant="caption">
                        Kullanıcı: {message.userExists
                          ? `${getUserRoleLabel(message.matchingSupportFlowUserRole)} / ${message.matchingSupportFlowUserIsActive ? 'aktif' : 'pasif'}`
                          : message.externalRequesterMatch
                            ? `Harici Müşteri / ${message.externalRequesterId ?? '-'}`
                            : 'eşleşme yok'} · Ek: {message.attachmentCount} · {message.isAlreadyProcessed ? 'işlenmiş' : 'işlenmemiş'}
                      </Typography>
                      {message.isCached && (
                        <Typography color="text.secondary" sx={{ display: 'block' }} variant="caption">
                          Önbellekten: {formatDate(message.cachedAt)}
                        </Typography>
                      )}
                    </Paper>
                  ))}
                </Stack>
              )}
            </Stack>
          </Paper>

          <Paper sx={{ border: '1px solid', borderColor: 'divider', p: 3 }}>
            <Stack spacing={2}>
              <Box>
                <Typography sx={{ fontWeight: 700 }} variant="h6">
                  Eylemler
                </Typography>
                <Typography color="text.secondary" sx={{ mt: 0.6 }} variant="body2">
                  OAuth akışını başlatın veya yetkilendirme tamamsa gelen mailleri işleyin.
                </Typography>
              </Box>

              <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5}>
                {needsAuthorization && (
                  <Button
                    disabled={authorizing}
                    onClick={() => void handleAuthorize()}
                    startIcon={
                      authorizing ? (
                        <CircularProgress aria-hidden="true" color="inherit" size={18} />
                      ) : (
                        <SendOutlined />
                      )
                    }
                    variant="contained"
                  >
                    {authorizing ? 'Bağlantı hazırlanıyor...' : 'Google ile Yetkilendir'}
                  </Button>
                )}
                {!needsAuthorization && (
                  <Button
                    disabled={processing || isRateLimited}
                    onClick={() => void handleProcessMailbox()}
                    startIcon={
                      processing ? (
                        <CircularProgress aria-hidden="true" color="inherit" size={18} />
                      ) : (
                        <MailOutlined />
                      )
                    }
                    variant="contained"
                  >
                    {processing ? 'İşleniyor...' : 'Gelen Mailleri İşle'}
                  </Button>
                )}
                <Button onClick={() => void loadStatus(true)} variant="outlined">
                  Durumu Yenile
                </Button>
              </Stack>
            </Stack>
          </Paper>

          <Paper sx={{ border: '1px solid', borderColor: 'divider', p: 3 }}>
            <Stack spacing={2}>
              <Typography sx={{ fontWeight: 700 }} variant="h6">
                İşleme Sonucu
              </Typography>

              {processSuccess && <Alert severity="success">{processSuccess}</Alert>}
              {processError && (
                <Alert severity={summary ? 'warning' : 'error'}>{processError}</Alert>
              )}

              {summary ? (
                <Stack spacing={2}>
                  <Box
                    sx={{
                      display: 'grid',
                      gap: 1.5,
                      gridTemplateColumns: {
                        xs: '1fr',
                        sm: 'repeat(2, minmax(0, 1fr))',
                        xl: 'repeat(4, minmax(0, 1fr))',
                      },
                    }}
                  >
                    {summaryLabels.map((item) => (
                      <MetricCard
                        accentColor={item.accent}
                        icon={<MailOutlined />}
                        key={item.key}
                        title={item.label}
                        value={summary[item.key]}
                      />
                    ))}
                  </Box>

                  {summary.results.filter((item) => item.reason).map((item, index) => (
                    <Alert key={`${item.status}-${item.ticketId ?? 'none'}-${index}`} severity="info">
                      {item.reason}
                    </Alert>
                  ))}
                </Stack>
              ) : (
                <EmptyStateCard
                  description="Henüz bir işleme sonucu yok. Gelen kutusu işleme tamamlandığında özet burada görünür."
                  icon={<MailOutlined />}
                  title="Sonuç bulunmuyor"
                />
              )}
            </Stack>
          </Paper>
        </>
      )}
    </Stack>
  )
}
