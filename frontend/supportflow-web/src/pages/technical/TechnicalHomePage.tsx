import {
  AssignmentTurnedInOutlined,
  EngineeringOutlined,
  ForumOutlined,
  GppBadOutlined,
  PlayCircleOutlined,
  TroubleshootOutlined,
  WarningAmberOutlined,
} from '@mui/icons-material'
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  List,
  ListItemButton,
  Paper,
  Stack,
  Typography,
} from '@mui/material'
import { useEffect, useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { EmptyStateCard } from '../../components/common/EmptyStateCard'
import { MetricCard } from '../../components/common/MetricCard'
import { PageHeader } from '../../components/common/PageHeader'
import { TicketPriorityChip } from '../../components/common/TicketPriorityChip'
import { TicketStatusChip } from '../../components/common/TicketStatusChip'
import { QueueAgingDashboardSection } from '../../components/dashboard/QueueAgingDashboardSection'
import { getQueueAgingDashboard } from '../../services/dashboardService'
import { getTicketDashboardSummary, getTickets } from '../../services/ticketService'
import type { QueueAgingDashboard, TicketDashboardSummary } from '../../types/dashboard'
import { TicketPriority, TicketStatus, type TicketListItem } from '../../types/ticket'
import { formatDate } from '../../utils/formatDate'

function getPriorityWeight(priority: TicketPriority) {
  if (priority === TicketPriority.Critical) return 4
  if (priority === TicketPriority.High) return 3
  if (priority === TicketPriority.Normal) return 2
  return 1
}

export function TechnicalHomePage() {
  const navigate = useNavigate()
  const [tickets, setTickets] = useState<TicketListItem[]>([])
  const [ticketSummary, setTicketSummary] = useState<TicketDashboardSummary | null>(null)
  const [queueAging, setQueueAging] = useState<QueueAgingDashboard | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [reloadKey, setReloadKey] = useState(0)

  useEffect(() => {
    let active = true

    async function loadTickets() {
      setLoading(true)
      setError('')
      try {
        const [response, ticketSummaryResponse, queueAgingResponse] = await Promise.all([
          getTickets({ activeOnly: true, pageSize: 25 }),
          getTicketDashboardSummary(),
          getQueueAgingDashboard(),
        ])
        if (active) {
          setTickets(response.items)
          setTicketSummary(ticketSummaryResponse)
          setQueueAging(queueAgingResponse)
        }
      } catch {
        if (active) {
          setError('Talepler yüklenemedi. Lütfen tekrar deneyin.')
        }
      } finally {
        if (active) {
          setLoading(false)
        }
      }
    }

    void loadTickets()
    return () => {
      active = false
    }
  }, [reloadKey])

  const summary = useMemo(() => {
    return {
      total: ticketSummary?.totalCount ?? 0,
      new: ticketSummary?.newCount ?? 0,
      inProgress: ticketSummary?.inProgressCount ?? 0,
      waitingForCustomer: ticketSummary?.waitingForCustomerCount ?? 0,
      resolved: ticketSummary?.resolvedOrClosedCount ?? 0,
      slaBreached: ticketSummary?.slaBreachedCount ?? 0,
      slaDueSoon: ticketSummary?.slaDueSoonCount ?? 0,
    }
  }, [ticketSummary])

  const pendingTickets = useMemo(
    () => {
      const activeStatuses: TicketStatus[] = [
        TicketStatus.New,
        TicketStatus.Assigned,
        TicketStatus.InProgress,
        TicketStatus.Reopened,
      ]

      return [...tickets]
        .filter((ticket) => activeStatuses.includes(ticket.status))
        .sort((first, second) => {
          const priorityDiff =
            getPriorityWeight(second.priority) - getPriorityWeight(first.priority)

          if (priorityDiff !== 0) {
            return priorityDiff
          }

          return new Date(second.updatedAt).getTime() - new Date(first.updatedAt).getTime()
        })
        .slice(0, 6)
    },
    [tickets],
  )

  return (
    <Stack spacing={3}>
      <PageHeader
        actions={
          <Button onClick={() => navigate('/technical/tickets')} variant="contained">
            Talepleri Aç
          </Button>
        }
        description="Operasyon kuyruğunu, öncelikli kayıtları ve aktif çözüm sürecini aynı panelden yönetin."
        eyebrow="Teknik Operasyon"
        title="Teknik Destek Paneli"
      />

      {loading ? (
        <Box sx={{ display: 'grid', minHeight: 280, placeItems: 'center' }}>
          <CircularProgress aria-label="Talepler yükleniyor" />
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
                lg: 'repeat(4, minmax(0, 1fr))',
              },
            }}
          >
            <MetricCard
              accentColor="#0f766e"
              description="Görünür destek kayıtlarının tamamı"
              icon={<TroubleshootOutlined />}
              title="Toplam Talep"
              value={summary.total}
            />
            <MetricCard
              accentColor="#2563eb"
              description="Yeni değerlendirme bekleyen kayıtlar"
              icon={<EngineeringOutlined />}
              title="Yeni"
              value={summary.new}
            />
            <MetricCard
              accentColor="#c2410c"
              description="Aktif çözüm süreci devam eden kayıtlar"
              icon={<PlayCircleOutlined />}
              title="Devam Eden"
              value={summary.inProgress}
            />
            <MetricCard
              accentColor="#7c3aed"
              description="Müşteri geri dönüşü bekleyen kayıtlar"
              icon={<ForumOutlined />}
              title="Müşteri Bekleyen"
              value={summary.waitingForCustomer}
            />
            <MetricCard
              accentColor="#15803d"
              description="Çözülen veya kapatılan talepler"
              icon={<AssignmentTurnedInOutlined />}
              title="Çözülen"
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
              description="SLA süresinin son %20'sindeki talepler"
              icon={<WarningAmberOutlined />}
              title="SLA Yaklaşıyor"
              value={summary.slaDueSoon}
            />
          </Box>

          {queueAging ? (
            <QueueAgingDashboardSection
              dashboard={queueAging}
              onOpenTicket={(ticketId) => navigate(`/technical/tickets/${ticketId}`)}
            />
          ) : null}

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
                  Bekleyen İşler
                </Typography>
                <Typography color="text.secondary" variant="body2">
                  Öncelik ve son güncellemeye göre öne çıkan açık talepler.
                </Typography>
              </Box>
              <Button onClick={() => navigate('/technical/tickets')} variant="outlined">
                Tüm Talepler
              </Button>
            </Box>

            {pendingTickets.length === 0 ? (
              <Box sx={{ p: 2.25 }}>
                <EmptyStateCard
                  description="İşlem bekleyen açık kayıt bulunmuyor."
                  icon={<TroubleshootOutlined />}
                  title="Kuyruk boş"
                />
              </Box>
            ) : (
              <List disablePadding>
                {pendingTickets.map((ticket, index) => (
                  <ListItemButton
                    divider={index < pendingTickets.length - 1}
                    key={ticket.id}
                    onClick={() => navigate(`/technical/tickets/${ticket.id}`)}
                    sx={{ alignItems: 'flex-start', gap: 2, px: 2.25, py: 1.75 }}
                  >
                    <Box sx={{ flexGrow: 1, minWidth: 0 }}>
                      <Typography color="text.secondary" variant="caption">
                        {ticket.ticketNumber} · {ticket.customerName}
                      </Typography>
                      <Typography sx={{ fontWeight: 700, mt: 0.4 }} variant="body2">
                        {ticket.title}
                      </Typography>
                      <Typography color="text.secondary" sx={{ mt: 0.6 }} variant="caption">
                        Atanan: {ticket.assignedUserName || 'Atanmadı'} ·{' '}
                        {formatDate(ticket.updatedAt)}
                      </Typography>
                    </Box>
                    <Stack
                      direction={{ xs: 'column', sm: 'row' }}
                      spacing={0.8}
                      sx={{ alignItems: { xs: 'flex-end', sm: 'center' } }}
                    >
                      <TicketStatusChip status={ticket.status} />
                      <TicketPriorityChip priority={ticket.priority} />
                    </Stack>
                  </ListItemButton>
                ))}
              </List>
            )}
          </Paper>
        </>
      )}
    </Stack>
  )
}
