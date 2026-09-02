import {
  AssignmentIndOutlined,
  EmailOutlined,
  MarkEmailReadOutlined,
  NotificationsNoneOutlined,
  SupportAgentOutlined,
} from '@mui/icons-material'
import {
  Alert,
  Box,
  Button,
  Chip,
  Paper,
  Stack,
  Typography,
} from '@mui/material'
import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { EmptyStateCard } from '../../components/common/EmptyStateCard'
import { PageHeader } from '../../components/common/PageHeader'
import { useAuth } from '../../hooks/useAuth'
import {
  getNotifications,
  getUnreadNotificationCount,
  markAllNotificationsAsRead,
  markNotificationAsRead,
} from '../../services/notificationService'
import { NotificationType, type AppNotification } from '../../types/notification'
import { formatRelativeDate } from '../../utils/formatRelativeDate'
import { getFriendlyApiErrorMessage } from '../../utils/getApiErrorMessage'
import { getDefaultRouteForRole } from '../../utils/roleRouting'

type NotificationFilter = 'all' | 'unread'

function getNotificationIcon(type: NotificationType) {
  switch (type) {
    case NotificationType.Assignment:
      return <AssignmentIndOutlined fontSize="small" />
    case NotificationType.GmailInbound:
    case NotificationType.GmailOutboundSuccess:
    case NotificationType.GmailOutboundFailure:
      return <EmailOutlined fontSize="small" />
    case NotificationType.TechnicalReply:
      return <MarkEmailReadOutlined fontSize="small" />
    default:
      return <SupportAgentOutlined fontSize="small" />
  }
}

export function NotificationsPage() {
  const { user } = useAuth()
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const [filter, setFilter] = useState<NotificationFilter>('all')
  const [notifications, setNotifications] = useState<AppNotification[]>([])
  const [unreadCount, setUnreadCount] = useState(0)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [highlightedNotificationId, setHighlightedNotificationId] = useState<number | null>(null)
  const notificationRefs = useRef(new Map<number, HTMLDivElement>())

  const basePath = user ? getDefaultRouteForRole(user.role) : '/login'
  const selectedNotificationId = useMemo(() => {
    const rawNotificationId = searchParams.get('notificationId')
    if (!rawNotificationId) {
      return null
    }

    const parsedNotificationId = Number(rawNotificationId)
    return Number.isInteger(parsedNotificationId) && parsedNotificationId > 0
      ? parsedNotificationId
      : null
  }, [searchParams])

  const loadNotifications = useCallback(async (selectedFilter: NotificationFilter) => {
    setLoading(true)
    setError('')

    try {
      const shouldKeepSelectedVisible =
        selectedFilter === 'unread' && selectedNotificationId !== null

      const [items, unread] = await Promise.all([
        getNotifications(shouldKeepSelectedVisible ? false : selectedFilter === 'unread'),
        getUnreadNotificationCount(),
      ])

      const nextNotifications = shouldKeepSelectedVisible
        ? items.filter(
            (item) => !item.isRead || item.id === selectedNotificationId,
          )
        : items

      setNotifications(nextNotifications)
      setUnreadCount(unread)
    } catch (error: unknown) {
      setError(
        getFriendlyApiErrorMessage(error, {
          fallback: 'Bildirimler yüklenemedi. Lütfen tekrar deneyin.',
          unavailableMessage:
            'SupportFlow hizmetine ulaşılamıyor. Lütfen daha sonra tekrar deneyin.',
        }),
      )
    } finally {
      setLoading(false)
    }
  }, [selectedNotificationId])

  useEffect(() => {
    void loadNotifications(filter)
  }, [filter, loadNotifications])

  useEffect(() => {
    if (selectedNotificationId === null || loading || notifications.length === 0) {
      return
    }

    const selectedElement = notificationRefs.current.get(selectedNotificationId)
    if (!selectedElement) {
      return
    }

    setHighlightedNotificationId(selectedNotificationId)
    selectedElement.scrollIntoView({ behavior: 'smooth', block: 'center' })

    const timeoutId = window.setTimeout(() => {
      setHighlightedNotificationId((currentHighlightedId) =>
        currentHighlightedId === selectedNotificationId ? null : currentHighlightedId,
      )
    }, 4500)

    return () => window.clearTimeout(timeoutId)
  }, [loading, notifications, selectedNotificationId])

  const isEmpty = useMemo(() => notifications.length === 0, [notifications.length])

  async function handleMarkAllRead() {
    await markAllNotificationsAsRead()
    await loadNotifications(filter)
  }

  async function handleNotificationClick(notification: AppNotification) {
    if (!notification.isRead) {
      await markNotificationAsRead(notification.id)
      setNotifications((currentNotifications) =>
        currentNotifications.map((currentNotification) =>
          currentNotification.id === notification.id
            ? { ...currentNotification, isRead: true }
            : currentNotification,
        ),
      )
      setUnreadCount((currentCount) => Math.max(0, currentCount - 1))
    }
  }

  function handleTicketNavigate(notification: AppNotification) {
    if (!notification.ticketId) {
      return
    }

    navigate(`${basePath}/tickets/${notification.ticketId}`)
  }

  return (
    <Stack spacing={3}>
      <PageHeader
        actions={
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1}>
            <Button
              onClick={() => setFilter('all')}
              variant={filter === 'all' ? 'contained' : 'outlined'}
            >
              Tümü
            </Button>
            <Button
              onClick={() => setFilter('unread')}
              variant={filter === 'unread' ? 'contained' : 'outlined'}
            >
              Okunmamış
            </Button>
            <Button onClick={() => void handleMarkAllRead()} variant="outlined">
              Tümünü Okundu İşaretle
            </Button>
          </Stack>
        }
        description="Ticket ve Gmail operasyon akışlarınıza ait ürün içi uyarılar burada listelenir."
        eyebrow="İletişim Merkezi"
        title="Bildirimler"
      />

      <Paper sx={{ border: '1px solid', borderColor: 'divider', p: 2.25 }}>
        <Stack direction="row" spacing={1} sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
          <Chip color="primary" label={`Okunmamış: ${unreadCount}`} />
          <Chip label={`Gösterim: ${filter === 'all' ? 'Tümü' : 'Sadece okunmamış'}`} variant="outlined" />
          {selectedNotificationId !== null ? (
            <Chip
              color="secondary"
              label="Seçilen bildirim görünür tutuluyor"
              variant="outlined"
            />
          ) : null}
        </Stack>
      </Paper>

      {loading ? (
        <Paper sx={{ border: '1px solid', borderColor: 'divider', p: 4 }}>
          <Typography color="text.secondary" variant="body2">
            Bildirimler yükleniyor...
          </Typography>
        </Paper>
      ) : error ? (
        <Alert severity="error">{error}</Alert>
      ) : isEmpty ? (
        <EmptyStateCard
          description="Bu görünüm için gösterilecek bildirim bulunmuyor."
          icon={<NotificationsNoneOutlined />}
          title="Bildirim bulunamadı"
        />
      ) : (
        <Stack spacing={1.2}>
          {notifications.map((notification) => (
            <Paper
              key={notification.id}
              ref={(element: HTMLDivElement | null) => {
                if (element) {
                  notificationRefs.current.set(notification.id, element)
                } else {
                  notificationRefs.current.delete(notification.id)
                }
              }}
              onClick={() => void handleNotificationClick(notification)}
              sx={{
                border: '1px solid',
                borderColor:
                  highlightedNotificationId === notification.id
                    ? 'secondary.main'
                    : notification.isRead
                      ? 'divider'
                      : 'primary.light',
                bgcolor:
                  highlightedNotificationId === notification.id
                    ? 'rgba(124, 58, 237, 0.12)'
                    : 'background.paper',
                cursor: notification.isRead ? 'default' : 'pointer',
                p: 2.25,
                transition: 'background-color 200ms ease, border-color 200ms ease',
              }}
            >
              <Stack direction="row" spacing={1.5} sx={{ alignItems: 'flex-start' }}>
                <Box
                  sx={{
                    alignItems: 'center',
                    bgcolor: notification.isRead ? 'rgba(124, 58, 237, 0.08)' : 'rgba(124, 58, 237, 0.16)',
                    borderRadius: 2.5,
                    color: 'primary.main',
                    display: 'grid',
                    height: 40,
                    placeItems: 'center',
                    width: 40,
                  }}
                >
                  {getNotificationIcon(notification.type)}
                </Box>
                <Box sx={{ flexGrow: 1, minWidth: 0 }}>
                  <Stack
                    direction={{ xs: 'column', sm: 'row' }}
                    spacing={1}
                    sx={{ justifyContent: 'space-between' }}
                  >
                    <Typography sx={{ fontWeight: notification.isRead ? 600 : 700 }} variant="body1">
                      {notification.title}
                    </Typography>
                    <Typography color="text.secondary" variant="caption">
                      {formatRelativeDate(notification.createdAt)}
                    </Typography>
                  </Stack>
                  <Typography color="text.secondary" sx={{ mt: 0.6 }} variant="body2">
                    {notification.message}
                  </Typography>
                  <Stack direction="row" spacing={1} sx={{ mt: 1.2 }}>
                    {!notification.isRead && <Chip label="Yeni" size="small" color="primary" />}
                    {notification.ticketId && (
                      <Chip label={`Talep #${notification.ticketId}`} size="small" variant="outlined" />
                    )}
                  </Stack>
                  {notification.ticketId ? (
                    <Box sx={{ mt: 1.4 }}>
                      <Button
                        onClick={(event) => {
                          event.stopPropagation()
                          handleTicketNavigate(notification)
                        }}
                        size="small"
                        variant="outlined"
                      >
                        Talebi Gör
                      </Button>
                    </Box>
                  ) : null}
                </Box>
              </Stack>
            </Paper>
          ))}
        </Stack>
      )}
    </Stack>
  )
}
