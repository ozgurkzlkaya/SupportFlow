import {
  AddCircleOutlined,
  AttachFile,
  ForumOutlined,
  LockOutlined,
  PersonAddAlt1,
  PriorityHigh,
  SellOutlined,
  SwapHoriz,
  Update,
} from '@mui/icons-material'
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Divider,
  Paper,
  Stack,
  Typography,
} from '@mui/material'
import { useEffect, useState } from 'react'
import { getTicketTimeline } from '../../services/ticketService'
import { UserRole, type UserRole as UserRoleType } from '../../types/auth'
import { TicketPriorityChip } from './TicketPriorityChip'
import { TicketStatusChip } from './TicketStatusChip'
import { PaginationControls } from './PaginationControls'
import {
  TicketTimelineKind,
  TicketTimelineSource,
  TicketTimelineVisibility,
  type TicketTimelineItem,
} from '../../types/timeline'
import { formatDate } from '../../utils/formatDate'
import { getFriendlyApiErrorMessage } from '../../utils/getApiErrorMessage'

interface TicketActivityTimelineProps {
  ticketId: number
  role: UserRoleType
}

type EventPresentation = {
  label: string
  icon: typeof AddCircleOutlined
  accent: string
}

const eventPresentations: Record<TicketTimelineKind, EventPresentation> = {
  [TicketTimelineKind.TicketCreated]: {
    label: 'Talep oluşturuldu',
    icon: AddCircleOutlined,
    accent: '#0f766e',
  },
  [TicketTimelineKind.StatusChanged]: {
    label: 'Durum değiştirildi',
    icon: SwapHoriz,
    accent: '#2563eb',
  },
  [TicketTimelineKind.PriorityChanged]: {
    label: 'Öncelik değiştirildi',
    icon: PriorityHigh,
    accent: '#d97706',
  },
  [TicketTimelineKind.AssignmentChanged]: {
    label: 'Atama değiştirildi',
    icon: PersonAddAlt1,
    accent: '#7c3aed',
  },
  [TicketTimelineKind.ClassificationChanged]: {
    label: 'Sınıflandırma güncellendi',
    icon: SellOutlined,
    accent: '#0f766e',
  },
  [TicketTimelineKind.MessageCreated]: {
    label: 'Mesaj gönderildi',
    icon: ForumOutlined,
    accent: '#2563eb',
  },
  [TicketTimelineKind.InternalNoteCreated]: {
    label: 'İç not eklendi',
    icon: LockOutlined,
    accent: '#b45309',
  },
  [TicketTimelineKind.AttachmentAdded]: {
    label: 'Dosya eklendi',
    icon: AttachFile,
    accent: '#475569',
  },
}

const genericPresentation: EventPresentation = {
  label: 'Talep güncellendi',
  icon: Update,
  accent: '#475569',
}

const customerAllowedKinds = new Set<TicketTimelineKind>([
  TicketTimelineKind.TicketCreated,
  TicketTimelineKind.StatusChanged,
  TicketTimelineKind.MessageCreated,
  TicketTimelineKind.AttachmentAdded,
])

function canRenderItem(item: TicketTimelineItem, role: UserRoleType): boolean {
  if (role !== UserRole.Customer) {
    return true
  }

  return item.visibility === TicketTimelineVisibility.RequesterVisible
    && customerAllowedKinds.has(item.kind)
}

function getActorRoleLabel(item: TicketTimelineItem): string | null {
  if (item.actor.kind === 'System') {
    return 'Sistem'
  }

  switch (item.actor.role) {
    case UserRole.Admin:
      return 'Yönetici'
    case UserRole.TechnicalStaff:
      return 'Teknik Personel'
    case UserRole.Customer:
      return 'Müşteri'
    default:
      return item.actor.kind === 'ExternalRequester' ? 'Harici Talep Sahibi' : null
  }
}

function getSourceLabel(source: TicketTimelineItem['source']): string | null {
  switch (source) {
    case TicketTimelineSource.Portal:
      return 'Portal'
    case TicketTimelineSource.GmailInbound:
      return 'E-posta'
    case TicketTimelineSource.System:
      return 'Sistem'
    default:
      return null
  }
}

function getAssignmentName(name: string | null): string {
  return name || 'Atanmamış'
}

function TimelineDetails({ item, role }: { item: TicketTimelineItem; role: UserRoleType }) {
  const metadata = item.metadata

  if (item.kind === TicketTimelineKind.StatusChanged && metadata?.oldStatus && metadata.newStatus) {
    return (
      <Stack direction="row" spacing={0.75} sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
        <TicketStatusChip status={metadata.oldStatus} />
        <SwapHoriz aria-label="durum değişimi" color="action" fontSize="small" />
        <TicketStatusChip status={metadata.newStatus} />
      </Stack>
    )
  }

  if (role !== UserRole.Customer && item.kind === TicketTimelineKind.PriorityChanged
    && metadata?.oldPriority && metadata.newPriority) {
    return (
      <Stack direction="row" spacing={0.75} sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
        <TicketPriorityChip priority={metadata.oldPriority} />
        <SwapHoriz aria-label="öncelik değişimi" color="action" fontSize="small" />
        <TicketPriorityChip priority={metadata.newPriority} />
      </Stack>
    )
  }

  if (role !== UserRole.Customer && item.kind === TicketTimelineKind.AssignmentChanged) {
    return (
      <Typography color="text.secondary" variant="body2">
        {getAssignmentName(metadata?.oldAssigneeName ?? null)} → {getAssignmentName(metadata?.newAssigneeName ?? null)}
      </Typography>
    )
  }

  if (role !== UserRole.Customer && item.kind === TicketTimelineKind.ClassificationChanged && metadata) {
    const hasCategoryChange = metadata.oldCategoryName !== metadata.newCategoryName
    const addedTags = metadata.addedTags ?? []
    const removedTags = metadata.removedTags ?? []

    return (
      <Stack spacing={0.75}>
        {hasCategoryChange ? (
          <Typography color="text.secondary" variant="body2">
            Kategori: {metadata.oldCategoryName || 'Sınıflandırılmamış'} → {metadata.newCategoryName || 'Sınıflandırılmamış'}
          </Typography>
        ) : null}
        {addedTags.length > 0 ? (
          <Stack direction="row" spacing={0.75} sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
            <Typography color="text.secondary" variant="caption">Etiket eklendi:</Typography>
            {addedTags.map((tag) => <Chip color="success" key={`added-${tag}`} label={tag} size="small" variant="outlined" />)}
          </Stack>
        ) : null}
        {removedTags.length > 0 ? (
          <Stack direction="row" spacing={0.75} sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
            <Typography color="text.secondary" variant="caption">Etiket kaldırıldı:</Typography>
            {removedTags.map((tag) => <Chip key={`removed-${tag}`} label={tag} size="small" variant="outlined" />)}
          </Stack>
        ) : null}
      </Stack>
    )
  }

  if (item.kind === TicketTimelineKind.AttachmentAdded && item.attachment) {
    return (
      <Typography color="text.secondary" sx={{ overflowWrap: 'anywhere' }} variant="body2">
        {item.attachment.fileName}
      </Typography>
    )
  }

  if (item.kind === TicketTimelineKind.MessageCreated) {
    return <Typography color="text.secondary" variant="body2">Yeni mesaj gönderildi.</Typography>
  }

  if (item.kind === TicketTimelineKind.InternalNoteCreated) {
    return <Typography color="text.secondary" variant="body2">İç not eklendi.</Typography>
  }

  return null
}

export function TicketActivityTimeline({ ticketId, role }: TicketActivityTimelineProps) {
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(25)
  const [items, setItems] = useState<TicketTimelineItem[]>([])
  const [pagination, setPagination] = useState<{
    page: number
    pageSize: number
    totalCount: number
    totalPages: number
    hasPreviousPage: boolean
    hasNextPage: boolean
  } | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [retryKey, setRetryKey] = useState(0)

  useEffect(() => {
    let active = true

    async function loadTimeline() {
      setLoading(true)
      setError('')
      try {
        const response = await getTicketTimeline(ticketId, page, pageSize)
        if (!active) return
        setItems(response.items)
        setPagination(response.pagination)
      } catch (requestError: unknown) {
        if (!active) return
        setItems([])
        setPagination(null)
        setError(getFriendlyApiErrorMessage(requestError, {
          fallback: 'Talep geçmişi yüklenemedi.',
          forbiddenMessage: 'Talep geçmişini görüntüleme yetkiniz bulunmuyor.',
          notFoundMessage: 'Talep geçmişi bulunamadı.',
        }))
      } finally {
        if (active) setLoading(false)
      }
    }

    void loadTimeline()
    return () => { active = false }
  }, [page, pageSize, retryKey, ticketId])

  const visibleItems = items.filter((item) => canRenderItem(item, role))

  return (
    <Paper component="section" aria-busy={loading} aria-labelledby="ticket-timeline-title" sx={{ border: '1px solid', borderColor: 'divider', p: { xs: 2, sm: 3 } }}>
      <Stack spacing={2.25}>
        <Box>
          <Typography id="ticket-timeline-title" sx={{ fontWeight: 700 }} variant="h6">
            Talep Geçmişi
          </Typography>
          <Typography color="text.secondary" sx={{ mt: 0.5 }} variant="body2">
            Talepteki önemli işlem ve iletişim hareketleri.
          </Typography>
        </Box>

        {loading ? (
          <Stack direction="row" spacing={1.25} sx={{ alignItems: 'center', minHeight: 96 }}>
            <CircularProgress aria-label="Talep geçmişi yükleniyor" size={22} />
            <Typography color="text.secondary" variant="body2">Talep geçmişi yükleniyor.</Typography>
          </Stack>
        ) : null}

        {!loading && error ? (
          <Alert action={<Button color="inherit" onClick={() => setRetryKey((key) => key + 1)}>Tekrar Dene</Button>} severity="error">
            {error}
          </Alert>
        ) : null}

        {!loading && !error && visibleItems.length === 0 ? (
          <Typography color="text.secondary" sx={{ py: 2, textAlign: 'center' }} variant="body2">
            Henüz görüntülenecek bir hareket bulunmuyor.
          </Typography>
        ) : null}

        {!loading && !error && visibleItems.length > 0 ? (
          <Stack component="ol" spacing={0} sx={{ listStyle: 'none', m: 0, p: 0 }}>
            {visibleItems.map((item, index) => {
              const presentation = eventPresentations[item.kind] ?? genericPresentation
              const EventIcon = presentation.icon
              const actorRoleLabel = getActorRoleLabel(item)
              const sourceLabel = getSourceLabel(item.source)

              return (
                <Box component="li" key={item.id} sx={{ display: 'grid', gap: 1.25, gridTemplateColumns: '32px minmax(0, 1fr)' }}>
                  <Box aria-hidden="true" sx={{ alignItems: 'center', display: 'flex', flexDirection: 'column', pt: 0.25 }}>
                    <Box sx={{ alignItems: 'center', bgcolor: `${presentation.accent}14`, border: '1px solid', borderColor: `${presentation.accent}42`, borderRadius: 99, color: presentation.accent, display: 'grid', flexShrink: 0, height: 28, placeItems: 'center', width: 28 }}>
                      <EventIcon fontSize="small" />
                    </Box>
                    {index < visibleItems.length - 1 ? <Box sx={{ bgcolor: 'divider', flexGrow: 1, my: 0.75, width: 2 }} /> : null}
                  </Box>

                  <Box sx={{ minWidth: 0, pb: index < visibleItems.length - 1 ? 2 : 0 }}>
                    <Stack spacing={1.1} sx={{ border: '1px solid', borderColor: item.kind === TicketTimelineKind.InternalNoteCreated ? 'warning.light' : 'divider', borderRadius: 2.5, p: { xs: 1.5, sm: 1.8 } }}>
                      <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} sx={{ alignItems: { sm: 'flex-start' }, justifyContent: 'space-between' }}>
                        <Box sx={{ minWidth: 0 }}>
                          <Stack direction="row" spacing={0.75} sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
                            <Typography sx={{ fontWeight: 700 }} variant="body2">{presentation.label}</Typography>
                            {item.kind === TicketTimelineKind.InternalNoteCreated ? <Chip color="warning" label="İç Not" size="small" /> : null}
                            {sourceLabel ? <Chip label={sourceLabel} size="small" variant="outlined" /> : null}
                          </Stack>
                          <Stack direction="row" spacing={0.75} sx={{ alignItems: 'center', flexWrap: 'wrap', mt: 0.45 }}>
                            <Typography color="text.secondary" variant="caption">{item.actor.displayName}</Typography>
                            {actorRoleLabel ? <Chip label={actorRoleLabel} size="small" variant="outlined" /> : null}
                          </Stack>
                        </Box>
                        <Typography color="text.secondary" sx={{ flexShrink: 0 }} variant="caption">{formatDate(item.occurredAt)}</Typography>
                      </Stack>
                      <Divider />
                      <TimelineDetails item={item} role={role} />
                    </Stack>
                  </Box>
                </Box>
              )
            })}
          </Stack>
        ) : null}

        {!loading && !error && pagination ? (
          <PaginationControls
            itemLabel="hareket"
            onPageChange={setPage}
            onPageSizeChange={(nextPageSize) => { setPageSize(nextPageSize); setPage(1) }}
            pagination={pagination}
          />
        ) : null}
      </Stack>
    </Paper>
  )
}
