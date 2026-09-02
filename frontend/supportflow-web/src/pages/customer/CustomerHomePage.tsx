import {
  Add,
  AssignmentTurnedInOutlined,
  ForumOutlined,
  PendingActionsOutlined,
  ReceiptLongOutlined,
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
import { useAuth } from '../../hooks/useAuth'
import { getTicketDashboardSummary } from '../../services/ticketService'
import type { TicketDashboardSummary } from '../../types/dashboard'
import { formatDate } from '../../utils/formatDate'

export function CustomerHomePage() {
  const { user } = useAuth()
  const navigate = useNavigate()
  const [ticketSummary, setTicketSummary] = useState<TicketDashboardSummary | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [reloadKey, setReloadKey] = useState(0)

  useEffect(() => {
    let active = true

    async function loadTickets() {
      setLoading(true)
      setError('')

      try {
        const response = await getTicketDashboardSummary()
        if (active) {
          setTicketSummary(response)
        }
      } catch {
        if (active) {
          setError('Talepleriniz yüklenemedi. Lütfen tekrar deneyin.')
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
      active: ticketSummary?.openCount ?? 0,
      waitingForYou: ticketSummary?.waitingForCustomerCount ?? 0,
      resolved: ticketSummary?.resolvedOrClosedCount ?? 0,
    }
  }, [ticketSummary])

  const recentTickets = useMemo(
    () =>
      ticketSummary?.recentTickets.slice(0, 5) ?? [],
    [ticketSummary],
  )

  return (
    <Stack spacing={3}>
      <PageHeader
        actions={
          <Button
            fullWidth
            onClick={() => navigate('/customer/tickets/new')}
            startIcon={<Add />}
            sx={{ width: { xs: '100%', md: 'auto' } }}
            variant="contained"
          >
            Yeni Talep Oluştur
          </Button>
        }
        description="Destek taleplerinizi, son güncellemeleri ve sizden yanıt bekleyen kayıtları buradan takip edin."
        eyebrow="Müşteri Paneli"
        title={`Hoş geldiniz, ${user?.firstName ?? 'Müşteri'}`}
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
                xl: 'repeat(4, minmax(0, 1fr))',
              },
            }}
          >
            <MetricCard
              accentColor="#2563eb"
              description="Sistemdeki toplam müşteri talebiniz"
              icon={<ReceiptLongOutlined />}
              title="Toplam Talep"
              value={summary.total}
            />
            <MetricCard
              accentColor="#0f766e"
              description="Aktif çözüm sürecindeki kayıtlar"
              icon={<PendingActionsOutlined />}
              title="Açık Talepler"
              value={summary.active}
            />
            <MetricCard
              accentColor="#c2410c"
              description="Geri dönüşünüz beklenen kayıtlar"
              icon={<ForumOutlined />}
              title="Sizden Yanıt Bekleyen"
              value={summary.waitingForYou}
            />
            <MetricCard
              accentColor="#15803d"
              description="Tamamlanan veya kapatılan talepler"
              icon={<AssignmentTurnedInOutlined />}
              title="Çözülen / Kapatılan"
              value={summary.resolved}
            />
          </Box>

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
                  Son Taleplerim
                </Typography>
                <Typography color="text.secondary" variant="body2">
                  En son güncellenen destek kayıtlarınıza hızlı erişin.
                </Typography>
              </Box>
              <Button onClick={() => navigate('/customer/tickets')} variant="outlined">
                Tümünü Görüntüle
              </Button>
            </Box>

            {recentTickets.length === 0 ? (
              <Box sx={{ p: 2.25 }}>
                <EmptyStateCard
                  actionLabel="İlk Talebi Oluştur"
                  description="Henüz destek kaydınız bulunmuyor. Yeni bir taleple süreci başlatabilirsiniz."
                  icon={<Add />}
                  onAction={() => navigate('/customer/tickets/new')}
                  title="Henüz talep yok"
                />
              </Box>
            ) : (
              <List disablePadding>
                {recentTickets.map((ticket, index) => (
                  <ListItemButton
                    divider={index < recentTickets.length - 1}
                    key={ticket.id}
                    onClick={() => navigate(`/customer/tickets/${ticket.id}`)}
                    sx={{ alignItems: 'flex-start', gap: 2, px: 2.25, py: 1.75 }}
                  >
                    <Box sx={{ flexGrow: 1, minWidth: 0 }}>
                      <Typography color="text.secondary" variant="caption">
                        {ticket.ticketNumber}
                      </Typography>
                      <Typography sx={{ fontWeight: 700, mt: 0.4 }} variant="body2">
                        {ticket.title}
                      </Typography>
                      <Typography color="text.secondary" sx={{ mt: 0.6 }} variant="caption">
                        Son güncelleme: {formatDate(ticket.updatedAt)}
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
