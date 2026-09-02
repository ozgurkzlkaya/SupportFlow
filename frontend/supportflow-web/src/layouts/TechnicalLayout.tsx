import {
  ConfirmationNumberOutlined,
  DashboardOutlined,
  EngineeringOutlined,
  ForumOutlined,
  NotificationsOutlined,
  PersonOutlined,
} from '@mui/icons-material'
import { AppShell, type ShellNavigationItem } from '../components/common/AppShell'

const navigationItems: ShellNavigationItem[] = [
  {
    label: 'Panel',
    path: '/technical',
    icon: <DashboardOutlined />,
    match: (pathname) => pathname === '/technical',
  },
  {
    label: 'Talepler',
    path: '/technical/tickets',
    icon: <ConfirmationNumberOutlined />,
  },
  {
    label: 'Sohbetler',
    path: '/technical/conversations',
    icon: <ForumOutlined />,
  },
  {
    label: 'Bildirimler',
    path: '/technical/notifications',
    icon: <NotificationsOutlined />,
  },
  {
    label: 'Profil',
    path: '/technical/profile',
    icon: <PersonOutlined />,
  },
]

export function TechnicalLayout() {
  return (
    <AppShell
      navigationItems={navigationItems}
      roleAccent="#6d28d9"
      roleDescription="Teknik operasyon"
      roleIcon={<EngineeringOutlined />}
      roleLabel="Teknik Personel"
    />
  )
}
