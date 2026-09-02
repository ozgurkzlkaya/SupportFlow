import {
  AddCircleOutlined,
  ConfirmationNumberOutlined,
  DashboardOutlined,
  NotificationsOutlined,
  PersonOutlined,
  SupportAgentOutlined,
} from '@mui/icons-material'
import { AppShell, type ShellNavigationItem } from '../components/common/AppShell'

const navigationItems: ShellNavigationItem[] = [
  {
    label: 'Panel',
    path: '/customer',
    icon: <DashboardOutlined />,
    match: (pathname) => pathname === '/customer',
  },
  {
    label: 'Taleplerim',
    path: '/customer/tickets',
    icon: <ConfirmationNumberOutlined />,
    match: (pathname) =>
      pathname === '/customer/tickets' ||
      (pathname.startsWith('/customer/tickets/') && pathname !== '/customer/tickets/new'),
  },
  {
    label: 'Yeni Talep',
    path: '/customer/tickets/new',
    icon: <AddCircleOutlined />,
  },
  {
    label: 'Bildirimler',
    path: '/customer/notifications',
    icon: <NotificationsOutlined />,
  },
  {
    label: 'Profil',
    path: '/customer/profile',
    icon: <PersonOutlined />,
  },
]

export function CustomerLayout() {
  return (
    <AppShell
      navigationItems={navigationItems}
      roleAccent="#7c3aed"
      roleDescription="Müşteri destek alanı"
      roleIcon={<SupportAgentOutlined />}
      roleLabel="Müşteri"
    />
  )
}
