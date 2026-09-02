import {
  ConfirmationNumberOutlined,
  DashboardOutlined,
  ForumOutlined,
  MailOutlined,
  NotificationsOutlined,
  PersonOutlined,
  PeopleOutlined,
  SellOutlined,
  ShieldOutlined,
} from '@mui/icons-material'
import { AppShell, type ShellNavigationItem } from '../components/common/AppShell'

const navigationItems: ShellNavigationItem[] = [
  {
    label: 'Panel',
    path: '/admin',
    icon: <DashboardOutlined />,
    match: (pathname) => pathname === '/admin',
  },
  {
    label: 'Talepler',
    path: '/admin/tickets',
    icon: <ConfirmationNumberOutlined />,
  },
  {
    label: 'Sohbetler',
    path: '/admin/conversations',
    icon: <ForumOutlined />,
  },
  {
    label: 'Kullanıcılar',
    path: '/admin/users',
    icon: <PeopleOutlined />,
  },
  {
    label: 'Gmail Entegrasyonu',
    path: '/admin/gmail',
    icon: <MailOutlined />,
  },
  {
    label: 'Sınıflandırma',
    path: '/admin/taxonomy',
    icon: <SellOutlined />,
  },
  {
    label: 'Bildirimler',
    path: '/admin/notifications',
    icon: <NotificationsOutlined />,
  },
  {
    label: 'Profil',
    path: '/admin/profile',
    icon: <PersonOutlined />,
  },
]

export function AdminLayout() {
  return (
    <AppShell
      navigationItems={navigationItems}
      roleAccent="#5b21b6"
      roleDescription="Yönetim merkezi"
      roleIcon={<ShieldOutlined />}
      roleLabel="Yönetici"
    />
  )
}
