import {
  AssignmentIndOutlined,
  AssignmentOutlined,
  ErrorOutlineOutlined,
  GroupsOutlined,
  MarkEmailUnreadOutlined,
  PersonOffOutlined,
  PriorityHighOutlined,
} from '@mui/icons-material'
import {
  Alert,
  Box,
  Button,
  LinearProgress,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography,
} from '@mui/material'
import type { AgentWorkloadAgent, AgentWorkloadDashboard } from '../../types/dashboard'
import { formatDuration } from '../../utils/formatDuration'
import { MetricCard } from '../common/MetricCard'

interface AgentWorkloadDashboardSectionProps {
  dashboard: AgentWorkloadDashboard | null
  error: string
  loading: boolean
  onOpenAgentTickets: (agentId: number) => void
  onOpenInactiveAssigneeTickets: () => void
  onOpenUnassignedTickets: () => void
}

interface WorkloadMetricProps {
  label: string
  value: number
  tone?: 'default' | 'warning' | 'danger'
}

function WorkloadMetric({ label, value, tone = 'default' }: WorkloadMetricProps) {
  const color = tone === 'danger' ? 'error.main' : tone === 'warning' ? 'warning.dark' : 'text.primary'

  return (
    <Stack spacing={0.2}>
      <Typography color="text.secondary" variant="caption">
        {label}
      </Typography>
      <Typography color={value > 0 ? color : 'text.secondary'} sx={{ fontWeight: 750 }} variant="body2">
        {value}
      </Typography>
    </Stack>
  )
}

function AgentWorkloadCard({
  agent,
  onOpenTickets,
}: {
  agent: AgentWorkloadAgent
  onOpenTickets: () => void
}) {
  return (
    <Paper sx={{ border: '1px solid', borderColor: 'divider', p: 2 }}>
      <Stack spacing={1.5}>
        <Box>
          <Typography sx={{ fontWeight: 750 }} variant="body2">
            {agent.fullName}
          </Typography>
          <Typography color="text.secondary" variant="caption">
            {agent.email}
          </Typography>
        </Box>
        <Box sx={{ display: 'grid', gap: 1.25, gridTemplateColumns: 'repeat(2, minmax(0, 1fr))' }}>
          <WorkloadMetric label="Aktif" value={agent.activeTicketCount} />
          <WorkloadMetric label="Kritik" tone="danger" value={agent.criticalTicketCount} />
          <WorkloadMetric label="Yüksek" tone="warning" value={agent.highPriorityTicketCount} />
          <WorkloadMetric label="Yanıt Bekleyen" value={agent.awaitingReplyCount} />
          <WorkloadMetric label="8+ Saat" tone="warning" value={agent.waitingOver8HoursCount} />
          <WorkloadMetric label="SLA İhlali" tone="danger" value={agent.slaBreachedCount} />
          <WorkloadMetric label="SLA Yaklaşıyor" tone="warning" value={agent.slaDueSoonCount} />
          <Stack spacing={0.2}>
            <Typography color="text.secondary" variant="caption">
              En Uzun Bekleme
            </Typography>
            <Typography sx={{ fontWeight: 750 }} variant="body2">
              {agent.longestAwaitingMinutes === null ? '—' : formatDuration(agent.longestAwaitingMinutes)}
            </Typography>
          </Stack>
        </Box>
        <Button onClick={onOpenTickets} size="small" sx={{ alignSelf: 'flex-start' }} variant="outlined">
          Talepleri Gör
        </Button>
      </Stack>
    </Paper>
  )
}

export function AgentWorkloadDashboardSection({
  dashboard,
  error,
  loading,
  onOpenAgentTickets,
  onOpenInactiveAssigneeTickets,
  onOpenUnassignedTickets,
}: AgentWorkloadDashboardSectionProps) {
  return (
    <Stack spacing={2}>
      <Box>
        <Typography sx={{ fontWeight: 800 }} variant="h6">
          Ekip İş Yükü
        </Typography>
        <Typography color="text.secondary" variant="body2">
          Aktif teknik personelin operasyon yoğunluğu, kuyruk beklemeleri ve SLA riski.
        </Typography>
      </Box>

      {loading ? (
        <Paper aria-label="Ekip iş yükü yükleniyor" sx={{ border: '1px solid', borderColor: 'divider', p: 2.25 }}>
          <LinearProgress />
        </Paper>
      ) : error ? (
        <Alert severity="warning">{error}</Alert>
      ) : dashboard ? (
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
              accentColor="#4f46e5"
              description="Açık operasyon kapsamındaki talepler"
              icon={<AssignmentOutlined />}
              title="Aktif Talep"
              value={dashboard.totals.activeTicketCount}
            />
            <Box
              aria-label="Atanmamış aktif talepleri görüntüle"
              onClick={onOpenUnassignedTickets}
              onKeyDown={(event) => {
                if (event.key === 'Enter' || event.key === ' ') {
                  event.preventDefault()
                  onOpenUnassignedTickets()
                }
              }}
              role="link"
              sx={{ borderRadius: 3, cursor: 'pointer', '&:focus-visible': { outline: '3px solid', outlineColor: 'primary.light' } }}
              tabIndex={0}
            >
              <MetricCard
                accentColor="#64748b"
                description="Henüz teknik personele yönlendirilmemiş talepler"
                icon={<AssignmentIndOutlined />}
                title="Atanmamış"
                value={dashboard.totals.unassignedTicketCount}
              />
            </Box>
            <MetricCard
              accentColor="#dc2626"
              description="Aktif kritik öncelikli talepler"
              icon={<PriorityHighOutlined />}
              title="Kritik"
              value={dashboard.totals.criticalTicketCount}
            />
            <MetricCard
              accentColor="#2563eb"
              description="Ekip yanıtı bekleyen aktif talepler"
              icon={<MarkEmailUnreadOutlined />}
              title="Yanıt Bekleyen"
              value={dashboard.totals.awaitingReplyCount}
            />
            <MetricCard
              accentColor="#be123c"
              description="SLA hedefi aşılan aktif talepler"
              icon={<ErrorOutlineOutlined />}
              title="SLA İhlali"
              value={dashboard.totals.slaBreachedCount}
            />
            <Box
              aria-label="Pasif personele atanmış aktif talepleri görüntüle"
              onClick={onOpenInactiveAssigneeTickets}
              onKeyDown={(event) => {
                if (event.key === 'Enter' || event.key === ' ') {
                  event.preventDefault()
                  onOpenInactiveAssigneeTickets()
                }
              }}
              role="link"
              sx={{ borderRadius: 3, cursor: 'pointer', '&:focus-visible': { outline: '3px solid', outlineColor: 'primary.light' } }}
              tabIndex={0}
            >
              <MetricCard
                accentColor={dashboard.totals.inactiveAssigneeTicketCount > 0 ? '#c2410c' : '#64748b'}
                description="Atanmamış olarak sayılmaz; yönetici incelemesi gerektirir"
                icon={<PersonOffOutlined />}
                title="Pasif Personele Atanmış"
                value={dashboard.totals.inactiveAssigneeTicketCount}
              />
            </Box>
          </Box>

          {dashboard.totals.inactiveAssigneeTicketCount > 0 ? (
            <Alert
              action={
                <Button color="inherit" onClick={onOpenInactiveAssigneeTickets} size="small">
                  Talepleri Gör
                </Button>
              }
              severity="warning"
            >
              Pasif personele atanmış aktif talepler var. Toplam: {dashboard.totals.inactiveAssigneeTicketCount}.
            </Alert>
          ) : null}

          <Paper sx={{ border: '1px solid', borderColor: 'divider' }}>
            <Box sx={{ borderBottom: '1px solid', borderColor: 'divider', p: 2.25 }}>
              <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
                <GroupsOutlined color="primary" />
                <Typography sx={{ fontWeight: 800 }} variant="h6">
                  Personel Dağılımı
                </Typography>
              </Stack>
              <Typography color="text.secondary" sx={{ mt: 0.5 }} variant="body2">
                En yüksek operasyon riski önce gelecek şekilde sıralanır.
              </Typography>
            </Box>

            {dashboard.agents.length === 0 ? (
              <Typography color="text.secondary" sx={{ p: 2.25 }} variant="body2">
                Aktif teknik personel bulunmuyor.
              </Typography>
            ) : (
              <>
                <TableContainer sx={{ display: { xs: 'none', md: 'block' } }}>
                  <Table aria-label="Teknik personel iş yükü tablosu" size="small">
                    <TableHead>
                      <TableRow>
                        <TableCell>Personel</TableCell>
                        <TableCell align="right">Aktif</TableCell>
                        <TableCell align="right">Kritik</TableCell>
                        <TableCell align="right">Yüksek</TableCell>
                        <TableCell align="right">Yanıt Bekleyen</TableCell>
                        <TableCell align="right">8+ Saat</TableCell>
                        <TableCell align="right">SLA İhlali</TableCell>
                        <TableCell align="right">SLA Yaklaşıyor</TableCell>
                        <TableCell align="right">En Uzun Bekleme</TableCell>
                      </TableRow>
                    </TableHead>
                    <TableBody>
                      {dashboard.agents.map((agent) => (
                        <TableRow
                          key={agent.userId}
                          hover
                          onClick={() => onOpenAgentTickets(agent.userId)}
                          onKeyDown={(event) => {
                            if (event.key === 'Enter' || event.key === ' ') {
                              event.preventDefault()
                              onOpenAgentTickets(agent.userId)
                            }
                          }}
                          role="link"
                          sx={{ cursor: 'pointer', '&:focus-visible': { outline: '3px solid', outlineColor: 'primary.light' } }}
                          tabIndex={0}
                        >
                          <TableCell>
                            <Typography sx={{ fontWeight: 700 }} variant="body2">
                              {agent.fullName}
                            </Typography>
                            <Typography color="text.secondary" variant="caption">
                              {agent.email}
                            </Typography>
                          </TableCell>
                          <TableCell align="right">{agent.activeTicketCount}</TableCell>
                          <TableCell align="right" sx={{ color: agent.criticalTicketCount > 0 ? 'error.main' : 'text.secondary', fontWeight: 700 }}>
                            {agent.criticalTicketCount}
                          </TableCell>
                          <TableCell align="right" sx={{ color: agent.highPriorityTicketCount > 0 ? 'warning.dark' : 'text.secondary', fontWeight: 700 }}>
                            {agent.highPriorityTicketCount}
                          </TableCell>
                          <TableCell align="right">{agent.awaitingReplyCount}</TableCell>
                          <TableCell align="right" sx={{ color: agent.waitingOver8HoursCount > 0 ? 'warning.dark' : 'text.secondary', fontWeight: 700 }}>
                            {agent.waitingOver8HoursCount}
                          </TableCell>
                          <TableCell align="right" sx={{ color: agent.slaBreachedCount > 0 ? 'error.main' : 'text.secondary', fontWeight: 700 }}>
                            {agent.slaBreachedCount}
                          </TableCell>
                          <TableCell align="right" sx={{ color: agent.slaDueSoonCount > 0 ? 'warning.dark' : 'text.secondary', fontWeight: 700 }}>
                            {agent.slaDueSoonCount}
                          </TableCell>
                          <TableCell align="right">
                            {agent.longestAwaitingMinutes === null ? '—' : formatDuration(agent.longestAwaitingMinutes)}
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </TableContainer>

                <Stack spacing={1.25} sx={{ display: { md: 'none' }, p: 1.5 }}>
                  {dashboard.agents.map((agent) => (
                    <AgentWorkloadCard
                      agent={agent}
                      key={agent.userId}
                      onOpenTickets={() => onOpenAgentTickets(agent.userId)}
                    />
                  ))}
                </Stack>
              </>
            )}
          </Paper>
        </>
      ) : null}
    </Stack>
  )
}
