import {
  Logout,
  Menu as MenuIcon,
  NotificationsOutlined,
  PasswordOutlined,
  PersonOutlined,
  KeyboardArrowDownRounded,
} from '@mui/icons-material'
import {
  Avatar,
  Box,
  Divider,
  Drawer,
  IconButton,
  List,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Menu,
  Stack,
  Toolbar,
  Typography,
} from '@mui/material'
import { useMemo, useState, type MouseEvent, type ReactNode } from 'react'
import { Outlet, useLocation, useNavigate } from 'react-router-dom'
import { SupportFlowLogo } from '../brand/SupportFlowLogo'
import { useAuth } from '../../hooks/useAuth'
import { NotificationBell } from './NotificationBell'
import { getDefaultRouteForRole } from '../../utils/roleRouting'

const drawerWidth = 280

export interface ShellNavigationItem {
  label: string
  path: string
  icon: ReactNode
  description?: string
  match?: (pathname: string) => boolean
}

interface AppShellProps {
  roleLabel: string
  roleDescription: string
  roleAccent: string
  roleIcon: ReactNode
  navigationItems: ShellNavigationItem[]
}

export function AppShell({
  roleLabel,
  roleDescription,
  roleAccent,
  roleIcon,
  navigationItems,
}: AppShellProps) {
  const { user, logout } = useAuth()
  const location = useLocation()
  const navigate = useNavigate()
  const [mobileOpen, setMobileOpen] = useState(false)
  const [accountAnchorEl, setAccountAnchorEl] = useState<HTMLElement | null>(null)

  const fullName = user ? `${user.firstName} ${user.lastName}` : ''
  const basePath = user ? getDefaultRouteForRole(user.role) : '/login'
  const userInitials = useMemo(() => {
    if (!user) {
      return 'S'
    }

    return `${user.firstName.charAt(0)}${user.lastName.charAt(0)}`.toLocaleUpperCase('tr-TR')
  }, [user])

  function isSelected(item: ShellNavigationItem) {
    if (item.match) {
      return item.match(location.pathname)
    }

    return item.path === location.pathname || location.pathname.startsWith(`${item.path}/`)
  }

  function goTo(path: string) {
    setMobileOpen(false)
    setAccountAnchorEl(null)
    navigate(path)
  }

  function handleLogout() {
    logout()
    setAccountAnchorEl(null)
    navigate('/login', { replace: true })
  }

  const drawerContent = (
    <Box
      sx={{
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        px: 1.5,
        py: 1.5,
      }}
    >
      <Stack
        direction="row"
        spacing={1.2}
        sx={{
          alignItems: 'center',
          borderRadius: 3,
          px: 1.2,
          py: 1.15,
        }}
      >
        <Avatar
          sx={{
            bgcolor: `${roleAccent}18`,
            boxShadow: `0 16px 28px ${roleAccent}20`,
            color: roleAccent,
            height: 44,
            width: 44,
          }}
        >
          {roleIcon}
        </Avatar>
        <Box sx={{ minWidth: 0 }}>
          <SupportFlowLogo
            sx={{ maxWidth: '100%', width: { xs: 168, lg: 182 } }}
            width={182}
          />
          <Typography color="text.secondary" noWrap sx={{ mt: 0.35 }} variant="caption">
            {roleDescription}
          </Typography>
        </Box>
      </Stack>

      <Divider sx={{ my: 1.5 }} />

      <List aria-label={`${roleLabel} menüsü`} sx={{ px: 0.35, py: 0 }}>
        {navigationItems.map((item) => {
          const selected = isSelected(item)

          return (
            <ListItemButton
              key={item.path}
              onClick={() => goTo(item.path)}
              selected={selected}
              sx={{
                borderRadius: 2.5,
                mb: 0.45,
                minHeight: 44,
                px: 1.1,
                '&.Mui-selected': {
                  backgroundColor: 'rgba(124, 58, 237, 0.1)',
                },
              }}
            >
              <ListItemIcon
                sx={{
                  color: selected ? roleAccent : 'text.secondary',
                  minWidth: 36,
                }}
              >
                {item.icon}
              </ListItemIcon>
              <ListItemText
                primary={
                  <Typography sx={{ fontWeight: selected ? 700 : 600 }} variant="body2">
                    {item.label}
                  </Typography>
                }
              />
            </ListItemButton>
          )
        })}
      </List>

      <Box sx={{ mt: 'auto', px: 0.35 }}>
        <Divider sx={{ mb: 1.5 }} />
        <Box
          sx={{
            background: 'linear-gradient(180deg, rgba(255,255,255,0.96), rgba(245,243,255,0.96))',
            border: '1px solid',
            borderColor: 'divider',
            borderRadius: 3,
            p: 1.25,
          }}
        >
          <Stack direction="row" spacing={1.05} sx={{ alignItems: 'center' }}>
            <Avatar
              sx={{
                bgcolor: `${roleAccent}18`,
                color: roleAccent,
                fontWeight: 700,
                height: 36,
                width: 36,
              }}
            >
              {userInitials}
            </Avatar>
            <Box sx={{ minWidth: 0 }}>
              <Typography noWrap sx={{ fontWeight: 700 }} variant="body2">
                {fullName}
              </Typography>
              <Typography color="text.secondary" noWrap variant="caption">
                {user?.email}
              </Typography>
            </Box>
          </Stack>
        </Box>

        <ListItemButton
          onClick={handleLogout}
          sx={{
            borderRadius: 2.5,
            mt: 1,
            minHeight: 42,
            px: 1.1,
          }}
        >
          <ListItemIcon sx={{ minWidth: 36 }}>
            <Logout />
          </ListItemIcon>
          <ListItemText
            primary={
              <Typography sx={{ fontWeight: 600 }} variant="body2">
                Çıkış Yap
              </Typography>
            }
          />
        </ListItemButton>
      </Box>
    </Box>
  )

  return (
    <Box sx={{ bgcolor: 'background.default', display: 'flex', minHeight: '100dvh' }}>
      <Box component="nav" sx={{ flexShrink: { lg: 0 }, width: { lg: drawerWidth } }}>
        <Drawer
          ModalProps={{ keepMounted: true }}
          onClose={() => setMobileOpen(false)}
          open={mobileOpen}
          sx={{
            display: { xs: 'block', lg: 'none' },
            '& .MuiDrawer-paper': {
              width: drawerWidth,
            },
          }}
          variant="temporary"
        >
          {drawerContent}
        </Drawer>
        <Drawer
          open
          sx={{
            display: { xs: 'none', lg: 'block' },
            '& .MuiDrawer-paper': {
              backdropFilter: 'blur(18px)',
              backgroundColor: 'rgba(255,255,255,0.9)',
              borderRightColor: 'divider',
              width: drawerWidth,
            },
          }}
          variant="permanent"
        >
          {drawerContent}
        </Drawer>
      </Box>

      <Box sx={{ display: 'flex', flexDirection: 'column', flexGrow: 1, minWidth: 0 }}>
        <Box
          component="header"
          sx={{
            backdropFilter: 'blur(18px)',
            backgroundColor: 'rgba(249, 247, 255, 0.84)',
            borderBottom: 1,
            borderColor: 'divider',
            position: 'sticky',
            top: 0,
            zIndex: (theme) => theme.zIndex.appBar,
          }}
        >
          <Toolbar sx={{ gap: 1.5, minHeight: { xs: 64, md: 68 }, px: { xs: 1.5, sm: 2.5, lg: 3 } }}>
            <IconButton
              aria-label="Menüyü aç"
              color="inherit"
              edge="start"
              onClick={() => setMobileOpen(true)}
              sx={{ display: { lg: 'none' } }}
            >
              <MenuIcon />
            </IconButton>

            <Box sx={{ flexGrow: 1, minWidth: 0, pl: { xs: 0.25, md: 0.5 } }}>
              <Typography color="text.secondary" sx={{ fontWeight: 700, letterSpacing: '0.04em' }} variant="caption">
                {roleLabel}
              </Typography>
              <Typography
                sx={{
                  color: 'text.primary',
                  fontSize: { xs: '1rem', sm: '1.1rem' },
                  fontWeight: 800,
                  letterSpacing: '-0.03em',
                  lineHeight: 1.15,
                  mt: 0.35,
                }}
              >
                SupportFlow
              </Typography>
            </Box>

            <Stack direction="row" spacing={0.6} sx={{ alignItems: 'center' }}>
              <NotificationBell />
              <ListItemButton
                onClick={(event: MouseEvent<HTMLElement>) => setAccountAnchorEl(event.currentTarget)}
                sx={{
                  borderRadius: 3,
                  display: 'flex',
                  gap: 1,
                  minHeight: 42,
                  px: 1,
                  width: 'auto',
                }}
              >
                <Avatar
                  sx={{
                    bgcolor: `${roleAccent}16`,
                    color: roleAccent,
                    fontWeight: 700,
                    height: 34,
                    width: 34,
                  }}
                >
                  {userInitials}
                </Avatar>
                <Box sx={{ display: { xs: 'none', md: 'block' }, minWidth: 0 }}>
                  <Typography noWrap sx={{ fontWeight: 700 }} variant="body2">
                    {fullName}
                  </Typography>
                  <Typography color="text.secondary" noWrap variant="caption">
                    {roleLabel}
                  </Typography>
                </Box>
                <KeyboardArrowDownRounded sx={{ color: 'text.secondary', display: { xs: 'none', md: 'block' } }} />
              </ListItemButton>
            </Stack>
          </Toolbar>
        </Box>

        <Menu
          anchorEl={accountAnchorEl}
          anchorOrigin={{ horizontal: 'right', vertical: 'bottom' }}
          onClose={() => setAccountAnchorEl(null)}
          open={Boolean(accountAnchorEl)}
          slotProps={{ paper: { sx: { borderRadius: 3, mt: 1, minWidth: 220 } } }}
          transformOrigin={{ horizontal: 'right', vertical: 'top' }}
        >
          <List disablePadding sx={{ py: 0.75 }}>
            <ListItemButton onClick={() => goTo(`${basePath}/profile`)}>
              <ListItemIcon sx={{ minWidth: 34 }}>
                <PersonOutlined fontSize="small" />
              </ListItemIcon>
              <ListItemText primary="Profilim" />
            </ListItemButton>
            <ListItemButton onClick={() => goTo(`${basePath}/notifications`)}>
              <ListItemIcon sx={{ minWidth: 34 }}>
                <NotificationsOutlined fontSize="small" />
              </ListItemIcon>
              <ListItemText primary="Bildirimler" />
            </ListItemButton>
            <ListItemButton onClick={() => goTo(`${basePath}/profile#password`)}>
              <ListItemIcon sx={{ minWidth: 34 }}>
                <PasswordOutlined fontSize="small" />
              </ListItemIcon>
              <ListItemText primary="Şifre Değiştir" />
            </ListItemButton>
            <Divider sx={{ my: 0.75 }} />
            <ListItemButton onClick={handleLogout}>
              <ListItemIcon sx={{ minWidth: 34 }}>
                <Logout fontSize="small" />
              </ListItemIcon>
              <ListItemText primary="Çıkış Yap" />
            </ListItemButton>
          </List>
        </Menu>

        <Box
          component="main"
          sx={{
            flexGrow: 1,
            minWidth: 0,
            px: { xs: 2, sm: 3, xl: 4 },
            py: { xs: 2.5, sm: 3, xl: 3.5 },
          }}
        >
          <Box sx={{ mx: 'auto', width: '100%', maxWidth: 1500 }}>
            <Outlet />
          </Box>
        </Box>
      </Box>
    </Box>
  )
}
