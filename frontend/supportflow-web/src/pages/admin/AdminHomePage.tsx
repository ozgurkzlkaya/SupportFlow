import {
  ConfirmationNumberOutlined,
  GppBadOutlined,
  MarkEmailReadOutlined,
  ShieldOutlined,
  SupportAgentOutlined,
  WarningAmberOutlined,
} from '@mui/icons-material'
import {
  Alert,
  Box,
  Button,
  Chip,
  LinearProgress,
  Paper,
  Stack,
  Typography,
} from '@mui/material'
import { useEffect, useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { MetricCard } from '../../components/common/MetricCard'
import { PageHeader } from '../../components/common/PageHeader'
import { TicketPriorityChip } from '../../components/common/TicketPriorityChip'
import { TicketStatusChip } from '../../components/common/TicketStatusChip'
import { AgentWorkloadDashboardSection } from '../../components/dashboard/AgentWorkloadDashboardSection'
import { QueueAgingDashboardSection } from '../../components/dashboard/QueueAgingDashboardSection'
import { getAgentWorkloadDashboard, getQueueAgingDashboard } from '../../services/dashboardService'
import { getTicketDashboardSummary } from '../../services/ticketService'
import type { AgentWorkloadDashboard, QueueAgingDashboard, TicketDashboardSummary } from '../../types/dashboard'
import { type TicketDashboardRecentItem } from '../../types/dashboard'
import { formatDate } from '../../utils/formatDate'

function SummaryRow({
  label,
  count,
  total,
}: {
  label: string
  count: number
  total: number
}) {
  const percentage = total === 0 ? 0 : Math.round((count / total) * 100)

  return (
    <Stack spacing={0.65}>
      <Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between' }}>
        <Typography variant="body2">{label}</Typography>
        <Typography color="text.secondary" variant="caption">
          {count} · %{percentage}
        </Typography>
      </Stack>
      <LinearProgress
        sx={{
          bgcolor: 'rgba(196, 181, 253, 0.28)',
          borderRadius: 99,
          height: 8,
        }}
        value={percentage}
        variant="determinate"
      />
    </Stack>
  )
}

export function AdminHomePage() {
  const navigate = useNavigate()
  const [ticketSummary, setTicketSummary] = useState<TicketDashboardSummary | null>(null)
  const [agentWorkload, setAgentWorkload] = useState<AgentWorkloadDashboard | null>(null)
  const [queueAging, setQueueAging] = useState<QueueAgingDashboard | null>(null)
  const [agentWorkloadError, setAgentWorkloadError] = useState('')
  const [agentWorkloadLoading, setAgentWorkloadLoading] = useState(true)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [reloadKey, setReloadKey] = useState(0)

  useEffect(() => {
    let active = true

    async function loadDashboard() {
      setLoading(true)
      setError('')
      setAgentWorkload(null)
      setAgentWorkloadError('')
      setAgentWorkloadLoading(true)

      void getAgentWorkloadDashboard()
        .then((response) => {
          if (active) {
            setAgentWorkload(response)
          }
        })
        .catch(() => {
          if (active) {
            setAgentWorkloadError('Ekip iş yükü bilgileri şu anda alınamadı.')
          }
        })
        .finally(() => {
          if (active) {
            setAgentWorkloadLoading(false)
          }
        })

      try {
        const [ticketSummaryResponse, queueAgingResponse] = await Promise.all([
          getTicketDashboardSummary(),
          getQueueAgingDashboard(),
        ])
        if (active) {
          setTicketSummary(ticketSummaryResponse)
          setQueueAging(queueAgingResponse)
        }
      } catch {
        if (active) {
          setError('Yönetici paneli yüklenemedi. Lütfen tekrar deneyin.')
        }
      } finally {
        if (active) {
          setLoading(false)
        }
      }
    }

    void loadDashboard()
    return () => {
      active = false
    }
  }, [reloadKey])

  const summary = useMemo(() => {
    const source = ticketSummary
    return {
      total: source?.totalCount ?? 0,
      open: source?.openCount ?? 0,
      waiting: source?.waitingForCustomerCount ?? 0,
      resolved: source?.resolvedOrClosedCount ?? 0,
      latest: source?.recentTickets ?? ([] as TicketDashboardRecentItem[]),
      distribution: {
        new: source?.newCount ?? 0,
        inProgress: source?.inProgressCount ?? 0,
        waiting: source?.waitingForCustomerCount ?? 0,
        reopened: source?.reopenedCount ?? 0,
        resolved: source?.resolvedOrClosedCount ?? 0,
      },
      priorities: {
        low: source?.lowPriorityCount ?? 0,
        normal: source?.normalPriorityCount ?? 0,
        high: source?.highPriorityCount ?? 0,
        critical: source?.criticalPriorityCount ?? 0,
      },
      slaBreached: source?.slaBreachedCount ?? 0,
      slaDueSoon: source?.slaDueSoonCount ?? 0,
      firstResponseBreached: source?.firstResponseBreachedCount ?? 0,
      resolutionBreached: source?.resolutionBreachedCount ?? 0,
    }
  }, [ticketSummary])

  return (
    <Stack spacing={3}>
      <PageHeader
        description="Talep operasyonunu, güncel yoğunluğu ve durum dağılımını tek panelden izleyin."
        eyebrow="Yönetim"
        title="Yönetici Paneli"
      />

      {loading ? (
        <Box sx={{ display: 'grid', minHeight: 260, placeItems: 'center' }}>
          <LinearProgress sx={{ width: 220 }} />
        </Box>
      ) : error ? (
        <Alert
          action={
            <Button color="inherit" onClick={() => setReloadKey((key) => key + 1)}>
              Tekrar Dene
            </Button>
          }
          severity="error"
        >
          {error}
        </Alert>
      ) : (
        <>
          <Box
            sx={{
              display: 'grid',
              gap: 2,
              gridTemplateColumns: {
                xs: '1fr',
                sm: 'repeat(2, minmax(0, 1fr))',
                lg: 'repeat(3, minmax(0, 1fr))',
              },
            }}
          >
            <MetricCard
              accentColor="#7c3aed"
              description="Sistemdeki toplam destek kaydı"
              icon={<ConfirmationNumberOutlined />}
              title="Toplam Talep"
              value={summary.total}
            />
            <MetricCard
              accentColor="#6d28d9"
              description="Aktif operasyon kuyruğu"
              icon={<SupportAgentOutlined />}
              title="Açık Talepler"
              value={summary.open}
            />
            <MetricCard
              accentColor="#4338ca"
              description="Müşteri geri dönüşü bekleyen kayıtlar"
              icon={<MarkEmailReadOutlined />}
              title="Müşteri Bekleyen"
              value={summary.waiting}
            />
            <MetricCard
              accentColor="#15803d"
              description="Çözülen veya kapatılan talepler"
              icon={<ShieldOutlined />}
              title="Çözülen / Kapatılan"
              value={summary.resolved}
            />
            <MetricCard
              accentColor="#dc2626"
              description="Herhangi bir SLA hedefi aşılmış talepler"
              icon={<GppBadOutlined />}
              title="SLA İhlali"
              value={summary.slaBreached}
            />
            <MetricCard
              accentColor="#c2410c"
              description="SLA süresinin son %20'sinde olan talepler"
              icon={<WarningAmberOutlined />}
              title="SLA Yaklaşıyor"
              value={summary.slaDueSoon}
            />
          </Box>

          {queueAging ? (
            <QueueAgingDashboardSection
              dashboard={queueAging}
              onOpenTicket={(ticketId) => navigate(`/admin/tickets/${ticketId}`)}
            />
          ) : null}

          <AgentWorkloadDashboardSection
            dashboard={agentWorkload}
            error={agentWorkloadError}
            loading={agentWorkloadLoading}
            onOpenAgentTickets={(agentId) =>
              navigate(`/admin/tickets?assignedUserId=${agentId}&activeOnly=true`)
            }
            onOpenInactiveAssigneeTickets={() =>
              navigate('/admin/tickets?assignmentState=InactiveAssignee&activeOnly=true')
            }
            onOpenUnassignedTickets={() =>
              navigate('/admin/tickets?assignmentState=Unassigned&activeOnly=true')
            }
          />

          <Box
            sx={{
              display: 'grid',
              gap: 2,
              gridTemplateColumns: { xs: '1fr', xl: 'minmax(0, 1.7fr) minmax(320px, 0.92fr)' },
            }}
          >
            <Paper sx={{ border: '1px solid', borderColor: 'divider' }}>
              <Box
                sx={{
                  alignItems: { md: 'center' },
                  borderBottom: '1px solid',
                  borderColor: 'divider',
                  display: 'flex',
                  flexDirection: { xs: 'column', md: 'row' },
                  gap: 1.25,
                  justifyContent: 'space-between',
                  p: 2.25,
                }}
              >
                <Box>
                  <Typography sx={{ fontWeight: 700 }} variant="h6">
                    Son Talepler
                  </Typography>
                  <Typography color="text.secondary" variant="body2">
                    Son güncellenen kayıtlar ve operasyon görünümü.
                  </Typography>
                </Box>
                <Button onClick={() => navigate('/admin/tickets')} variant="outlined">
                  Tüm Talepleri Görüntüle
                </Button>
              </Box>

              <Stack divider={<Box sx={{ borderBottom: '1px solid', borderColor: 'divider' }} />}>
                {summary.latest.map((ticket) => (
                  <Box
                    key={ticket.id}
                    onClick={() => navigate(`/admin/tickets/${ticket.id}`)}
                    onKeyDown={(event) => {
                      if (event.key === 'Enter' || event.key === ' ') {
                        event.preventDefault()
                        navigate(`/admin/tickets/${ticket.id}`)
                      }
                    }}
                    role="link"
                    sx={{
                      cursor: 'pointer',
                      p: 2.25,
                      transition: 'background-color 120ms ease',
                      '&:hover': {
                        bgcolor: 'rgba(124, 58, 237, 0.05)',
                      },
                      '&:focus-visible': {
                        outline: '3px solid',
                        outlineColor: 'primary.light',
                      },
                    }}
                    tabIndex={0}
                  >
                    <Stack
                      direction={{ xs: 'column', md: 'row' }}
                      spacing={1.25}
                      sx={{ justifyContent: 'space-between' }}
                    >
                      <Box sx={{ flexGrow: 1, minWidth: 0 }}>
                        <Typography sx={{ fontWeight: 700 }} variant="body2">
                          {ticket.ticketNumber}
                        </Typography>
                        <Typography sx={{ mt: 0.35 }} variant="body2">
                          {ticket.title}
                        </Typography>
                        <Typography color="text.secondary" sx={{ mt: 0.5 }} variant="caption">
                          {ticket.customerName} · {formatDate(ticket.updatedAt)}
                        </Typography>
                      </Box>
                      <Stack
                        direction={{ xs: 'column', sm: 'row' }}
                        spacing={0.8}
                        sx={{ alignItems: { xs: 'flex-start', sm: 'center' } }}
                      >
                        <TicketStatusChip status={ticket.status} />
                        <TicketPriorityChip priority={ticket.priority} />
                      </Stack>
                    </Stack>
                  </Box>
                ))}
              </Stack>
            </Paper>

            <Stack spacing={2}>
              <Paper sx={{ border: '1px solid', borderColor: 'divider', p: 2.25 }}>
                <Typography sx={{ fontWeight: 700, mb: 2 }} variant="h6">
                  Durum Dağılımı
                </Typography>
                <Stack spacing={1.5}>
                  <SummaryRow count={summary.distribution.new} label="Yeni" total={summary.total} />
                  <SummaryRow
                    count={summary.distribution.inProgress}
                    label="Devam Ediyor"
                    total={summary.total}
                  />
                  <SummaryRow
                    count={summary.distribution.waiting}
                    label="Müşteri Bekleniyor"
                    total={summary.total}
                  />
                  <SummaryRow
                    count={summary.distribution.reopened}
                    label="Yeniden Açıldı"
                    total={summary.total}
                  />
                  <SummaryRow
                    count={summary.distribution.resolved}
                    label="Çözüldü"
                    total={summary.total}
                  />
                </Stack>
              </Paper>

              <Paper sx={{ border: '1px solid', borderColor: 'divider', p: 2.25 }}>
                <Stack spacing={1.5}>
                  <Typography sx={{ fontWeight: 700 }} variant="h6">
                    Öncelik Dağılımı
                  </Typography>
                  <Stack direction="row" spacing={1} sx={{ flexWrap: 'wrap' }}>
                    <Chip label={`Düşük: ${summary.priorities.low}`} variant="outlined" />
                    <Chip label={`Normal: ${summary.priorities.normal}`} variant="outlined" />
                    <Chip color="warning" label={`Yüksek: ${summary.priorities.high}`} />
                    <Chip color="error" label={`Kritik: ${summary.priorities.critical}`} />
                  </Stack>
                  <Stack direction="row" spacing={1} sx={{ flexWrap: 'wrap' }}>
                    <Chip
                      color="error"
                      label={`İlk Yanıt İhlali: ${summary.firstResponseBreached}`}
                      size="small"
                      variant="outlined"
                    />
                    <Chip
                      color="error"
                      label={`Çözüm İhlali: ${summary.resolutionBreached}`}
                      size="small"
                      variant="outlined"
                    />
                  </Stack>
                  <Typography color="text.secondary" variant="body2">
                    Bu özet yalnızca mevcut ticket verisinden üretilir ve ayrı bir Gmail sağlık
                    widget’ı içermez.
                  </Typography>
                </Stack>
              </Paper>
            </Stack>
          </Box>
        </>
      )}
    </Stack>
  )
}
