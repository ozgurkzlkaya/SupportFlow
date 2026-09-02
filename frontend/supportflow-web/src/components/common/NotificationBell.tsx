import {
  AssignmentIndOutlined,
  EmailOutlined,
  MarkEmailReadOutlined,
  NotificationsNoneOutlined,
  SupportAgentOutlined,
} from '@mui/icons-material'
import {
  Badge,
  Box,
  Button,
  Divider,
  IconButton,
  List,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Menu,
  Stack,
  Typography,
} from '@mui/material'
import { useCallback, useEffect, useMemo, useState, type MouseEvent } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '../../hooks/useAuth'
import {
  getNotifications,
  getUnreadNotificationCount,
  markNotificationAsRead,
} from '../../services/notificationService'
import { NotificationType, type AppNotification } from '../../types/notification'
import { formatRelativeDate } from '../../utils/formatRelativeDate'
import { getDefaultRouteForRole } from '../../utils/roleRouting'

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

export function NotificationBell() {
  const { user } = useAuth()
  const location = useLocation()
  const navigate = useNavigate()
  const [anchorEl, setAnchorEl] = useState<HTMLElement | null>(null)
  const [notifications, setNotifications] = useState<AppNotification[]>([])
  const [unreadCount, setUnreadCount] = useState(0)

  const basePath = user ? getDefaultRouteForRole(user.role) : '/login'
  const open = Boolean(anchorEl)

  const refreshUnreadCount = useCallback(async () => {
    if (!user) {
      return
    }

    try {
      const count = await getUnreadNotificationCount()
      setUnreadCount(count)
    } catch {
      setUnreadCount(0)
    }
  }, [user])

  const refreshMenu = useCallback(async () => {
    if (!user) {
      return
    }

    try {
      const items = await getNotifications()
      setNotifications(items.slice(0, 5))
      setUnreadCount(items.filter((item) => !item.isRead).length)
    } catch {
      setNotifications([])
    }
  }, [user])

  useEffect(() => {
    if (!user) {
      return
    }

    void refreshUnreadCount()

    const timer = window.setInterval(() => {
      void refreshUnreadCount()
    }, 45000)

    return () => window.clearInterval(timer)
  }, [refreshUnreadCount, user])

  useEffect(() => {
    void refreshUnreadCount()
  }, [location.pathname, refreshUnreadCount])

  const empty = useMemo(() => notifications.length === 0, [notifications.length])

  async function handleNotificationClick(notification: AppNotification) {
    if (!notification.isRead) {
      await markNotificationAsRead(notification.id)
    }

    setNotifications((currentNotifications) =>
      currentNotifications.map((currentNotification) =>
        currentNotification.id === notification.id
          ? { ...currentNotification, isRead: true }
          : currentNotification,
      ),
    )
    setUnreadCount((current) =>
      notification.isRead ? current : Math.max(0, current - 1),
    )
    setAnchorEl(null)
    navigate(`${basePath}/notifications?notificationId=${notification.id}`)
  }

  async function handleOpen(event: MouseEvent<HTMLElement>) {
    setAnchorEl(event.currentTarget)
    await refreshMenu()
  }

  return (
    <>
      <IconButton
        aria-label="Bildirimler"
        color="inherit"
        onClick={(event) => void handleOpen(event)}
      >
        <Badge badgeContent={unreadCount} color="primary" max={99}>
          <NotificationsNoneOutlined />
        </Badge>
      </IconButton>

      <Menu
        anchorEl={anchorEl}
        anchorOrigin={{ horizontal: 'right', vertical: 'bottom' }}
        onClose={() => setAnchorEl(null)}
        open={open}
        slotProps={{
          paper: {
            sx: {
              borderRadius: 3,
              mt: 1,
              width: { xs: 'calc(100vw - 24px)', sm: 360 },
              maxWidth: 360,
            },
          },
        }}
        transformOrigin={{ horizontal: 'right', vertical: 'top' }}
      >
        <Box sx={{ px: 2, py: 1.5 }}>
          <Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between' }}>
            <Box>
              <Typography sx={{ fontWeight: 700 }} variant="body1">
                Bildirimler
              </Typography>
              <Typography color="text.secondary" variant="caption">
                Okunmamış: {unreadCount}
              </Typography>
            </Box>
            <Button onClick={() => navigate(`${basePath}/notifications`)} size="small">
              Tüm Bildirimleri Gör
            </Button>
          </Stack>
        </Box>
        <Divider />

        {empty ? (
          <Box sx={{ px: 2.5, py: 3 }}>
            <Typography color="text.secondary" variant="body2">
              Yeni bildiriminiz bulunmuyor.
            </Typography>
          </Box>
        ) : (
          <List disablePadding sx={{ py: 0.5 }}>
            {notifications.map((notification) => (
              <ListItemButton
                key={notification.id}
                onClick={() => void handleNotificationClick(notification)}
                sx={{
                  alignItems: 'flex-start',
                  bgcolor: notification.isRead ? 'transparent' : 'rgba(124, 58, 237, 0.06)',
                  border: '1px solid',
                  borderColor: notification.isRead ? 'transparent' : 'rgba(124, 58, 237, 0.12)',
                  borderRadius: 2.5,
                  mb: 0.5,
                  mx: 0.75,
                }}
              >
                <ListItemIcon sx={{ color: 'primary.main', minWidth: 36, mt: 0.3 }}>
                  {getNotificationIcon(notification.type)}
                </ListItemIcon>
                <ListItemText
                  primary={
                    <Typography
                      sx={{ fontWeight: notification.isRead ? 600 : 700 }}
                      variant="body2"
                    >
                      {notification.title}
                    </Typography>
                  }
                  secondary={
                    <Stack spacing={0.45} sx={{ mt: 0.35 }}>
                      <Typography color="text.secondary" variant="caption">
                        {notification.message}
                      </Typography>
                      <Typography color="text.secondary" variant="caption">
                        {formatRelativeDate(notification.createdAt)}
                      </Typography>
                    </Stack>
                  }
                />
              </ListItemButton>
            ))}
          </List>
        )}
      </Menu>
    </>
  )
}
