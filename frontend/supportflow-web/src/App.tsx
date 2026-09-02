import { Box, CircularProgress } from '@mui/material'
import { lazy, Suspense } from 'react'
import { Navigate, Route, Routes } from 'react-router-dom'
import { useAuth } from './hooks/useAuth'
import { LoginPage } from './pages/auth/LoginPage'
import { RegisterPage } from './pages/auth/RegisterPage'
import { UnauthorizedPage } from './pages/UnauthorizedPage'
import { ProtectedRoute } from './routes/ProtectedRoute'
import { UserRole } from './types/auth'
import { getDefaultRouteForRole } from './utils/roleRouting'

const CustomerLayout = lazy(() =>
  import('./layouts/CustomerLayout').then((module) => ({
    default: module.CustomerLayout,
  })),
)
const CustomerHomePage = lazy(() =>
  import('./pages/customer/CustomerHomePage').then((module) => ({
    default: module.CustomerHomePage,
  })),
)
const CustomerTicketsPage = lazy(() =>
  import('./pages/customer/CustomerTicketsPage').then((module) => ({
    default: module.CustomerTicketsPage,
  })),
)
const CreateTicketPage = lazy(() =>
  import('./pages/customer/CreateTicketPage').then((module) => ({
    default: module.CreateTicketPage,
  })),
)
const CustomerTicketDetailPage = lazy(() =>
  import('./pages/customer/CustomerTicketDetailPage').then((module) => ({
    default: module.CustomerTicketDetailPage,
  })),
)
const TechnicalLayout = lazy(() =>
  import('./layouts/TechnicalLayout').then((module) => ({
    default: module.TechnicalLayout,
  })),
)
const TechnicalHomePage = lazy(() =>
  import('./pages/technical/TechnicalHomePage').then((module) => ({
    default: module.TechnicalHomePage,
  })),
)
const TechnicalTicketsPage = lazy(() =>
  import('./pages/technical/TechnicalTicketsPage').then((module) => ({
    default: module.TechnicalTicketsPage,
  })),
)
const TechnicalTicketDetailPage = lazy(() =>
  import('./pages/technical/TechnicalTicketDetailPage').then((module) => ({
    default: module.TechnicalTicketDetailPage,
  })),
)
const AdminLayout = lazy(() =>
  import('./layouts/AdminLayout').then((module) => ({
    default: module.AdminLayout,
  })),
)
const AdminHomePage = lazy(() =>
  import('./pages/admin/AdminHomePage').then((module) => ({
    default: module.AdminHomePage,
  })),
)
const GmailIntegrationPage = lazy(() =>
  import('./pages/admin/GmailIntegrationPage').then((module) => ({
    default: module.GmailIntegrationPage,
  })),
)
const AdminUsersPage = lazy(() =>
  import('./pages/admin/AdminUsersPage').then((module) => ({
    default: module.AdminUsersPage,
  })),
)
const AdminUserDetailPage = lazy(() =>
  import('./pages/admin/AdminUserDetailPage').then((module) => ({
    default: module.AdminUserDetailPage,
  })),
)
const AdminTaxonomyPage = lazy(() =>
  import('./pages/admin/AdminTaxonomyPage').then((module) => ({
    default: module.AdminTaxonomyPage,
  })),
)
const ProfilePage = lazy(() =>
  import('./pages/shared/ProfilePage').then((module) => ({
    default: module.ProfilePage,
  })),
)
const NotificationsPage = lazy(() =>
  import('./pages/shared/NotificationsPage').then((module) => ({
    default: module.NotificationsPage,
  })),
)
const ConversationsPage = lazy(() =>
  import('./pages/shared/ConversationsPage').then((module) => ({
    default: module.ConversationsPage,
  })),
)

function RouteLoading() {
  return (
    <Box sx={{ display: 'grid', minHeight: '50vh', placeItems: 'center' }}>
      <CircularProgress aria-label="Sayfa yükleniyor" />
    </Box>
  )
}

function HomeRedirect() {
  const { user, loading } = useAuth()

  if (loading) {
    return (
      <Box sx={{ display: 'grid', minHeight: '100vh', placeItems: 'center' }}>
        <CircularProgress aria-label="Oturum yükleniyor" />
      </Box>
    )
  }

  if (!user) {
    return <Navigate replace to="/login" />
  }

  return <Navigate replace to={getDefaultRouteForRole(user.role)} />
}

function App() {
  return (
    <Routes>
      <Route element={<HomeRedirect />} path="/" />
      <Route element={<LoginPage />} path="/login" />
      <Route element={<RegisterPage />} path="/register" />
      <Route element={<UnauthorizedPage />} path="/unauthorized" />

      <Route element={<ProtectedRoute allowedRoles={[UserRole.Customer]} />}>
        <Route
          element={
            <Suspense fallback={<RouteLoading />}>
              <CustomerLayout />
            </Suspense>
          }
          path="/customer"
        >
          <Route
            element={
              <Suspense fallback={<RouteLoading />}>
                <CustomerHomePage />
              </Suspense>
            }
            index
          />
          <Route
            element={
              <Suspense fallback={<RouteLoading />}>
                <CustomerTicketsPage />
              </Suspense>
            }
            path="tickets"
          />
          <Route
            element={
              <Suspense fallback={<RouteLoading />}>
                <CreateTicketPage />
              </Suspense>
            }
            path="tickets/new"
          />
          <Route
            element={
              <Suspense fallback={<RouteLoading />}>
                <CustomerTicketDetailPage />
              </Suspense>
            }
            path="tickets/:id"
          />
          <Route
            element={
              <Suspense fallback={<RouteLoading />}>
                <NotificationsPage />
              </Suspense>
            }
            path="notifications"
          />
          <Route
            element={
              <Suspense fallback={<RouteLoading />}>
                <ProfilePage />
              </Suspense>
            }
            path="profile"
          />
        </Route>
      </Route>

      <Route element={<ProtectedRoute allowedRoles={[UserRole.TechnicalStaff]} />}>
        <Route
          element={
            <Suspense fallback={<RouteLoading />}>
              <TechnicalLayout />
            </Suspense>
          }
          path="/technical"
        >
          <Route
            element={
              <Suspense fallback={<RouteLoading />}>
                <TechnicalHomePage />
              </Suspense>
            }
            index
          />
          <Route
            element={
              <Suspense fallback={<RouteLoading />}>
                <TechnicalTicketsPage />
              </Suspense>
            }
            path="tickets"
          />
          <Route
            element={
              <Suspense fallback={<RouteLoading />}>
                <TechnicalTicketDetailPage />
              </Suspense>
            }
            path="tickets/:id"
          />
          <Route
            element={
              <Suspense fallback={<RouteLoading />}>
                <ConversationsPage />
              </Suspense>
            }
            path="conversations"
          />
          <Route
            element={
              <Suspense fallback={<RouteLoading />}>
                <NotificationsPage />
              </Suspense>
            }
            path="notifications"
          />
          <Route
            element={
              <Suspense fallback={<RouteLoading />}>
                <ProfilePage />
              </Suspense>
            }
            path="profile"
          />
        </Route>
      </Route>

      <Route element={<ProtectedRoute allowedRoles={[UserRole.Admin]} />}>
        <Route
          element={
            <Suspense fallback={<RouteLoading />}>
              <AdminLayout />
            </Suspense>
          }
          path="/admin"
        >
          <Route
            element={
              <Suspense fallback={<RouteLoading />}>
                <AdminHomePage />
              </Suspense>
            }
            index
          />
          <Route
            element={
              <Suspense fallback={<RouteLoading />}>
                <TechnicalTicketsPage />
              </Suspense>
            }
            path="tickets"
          />
          <Route
            element={
              <Suspense fallback={<RouteLoading />}>
                <TechnicalTicketDetailPage />
              </Suspense>
            }
            path="tickets/:id"
          />
          <Route
            element={
              <Suspense fallback={<RouteLoading />}>
                <ConversationsPage />
              </Suspense>
            }
            path="conversations"
          />
          <Route
            element={
              <Suspense fallback={<RouteLoading />}>
                <AdminUsersPage />
              </Suspense>
            }
            path="users"
          />
          <Route
            element={
              <Suspense fallback={<RouteLoading />}>
                <AdminUserDetailPage />
              </Suspense>
            }
            path="users/:id"
          />
          <Route
            element={
              <Suspense fallback={<RouteLoading />}>
                <AdminTaxonomyPage />
              </Suspense>
            }
            path="taxonomy"
          />
          <Route
            element={
              <Suspense fallback={<RouteLoading />}>
                <GmailIntegrationPage />
              </Suspense>
            }
            path="gmail"
          />
          <Route
            element={
              <Suspense fallback={<RouteLoading />}>
                <NotificationsPage />
              </Suspense>
            }
            path="notifications"
          />
          <Route
            element={
              <Suspense fallback={<RouteLoading />}>
                <ProfilePage />
              </Suspense>
            }
            path="profile"
          />
        </Route>
      </Route>

      <Route element={<Navigate replace to="/" />} path="*" />
    </Routes>
  )
}

export default App
