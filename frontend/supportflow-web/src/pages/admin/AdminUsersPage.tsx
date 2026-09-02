import { FilterAltOutlined, Search } from '@mui/icons-material'
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  FormControl,
  InputAdornment,
  InputLabel,
  LinearProgress,
  MenuItem,
  Paper,
  Select,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Typography,
  type SelectChangeEvent,
} from '@mui/material'
import { useEffect, useRef, useState, type KeyboardEvent } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { EmptyStateCard } from '../../components/common/EmptyStateCard'
import { PageHeader } from '../../components/common/PageHeader'
import { PaginationControls } from '../../components/common/PaginationControls'
import { UserAccountStatusChip } from '../../components/common/UserAccountStatusChip'
import { UserRoleChip } from '../../components/common/UserRoleChip'
import { getAdminUsers } from '../../services/adminUserService'
import {
  UserAccountStatusFilter,
  isAdminUserRole,
  isUserAccountStatusFilter,
  type AdminUserListItem,
} from '../../types/adminUser'
import { UserRole, type UserRole as UserRoleType } from '../../types/auth'
import type { PaginationMetadata } from '../../types/pagination'
import { formatDate } from '../../utils/formatDate'
import { getUserRoleLabel } from '../../utils/userLabels'

type RoleFilter = UserRoleType | 'All'
type StatusFilter = UserAccountStatusFilter | 'All'

interface UserListViewState {
  search: string
  role: RoleFilter
  status: StatusFilter
  page: number
  pageSize: number
}

const emptyPagination: PaginationMetadata = {
  page: 1,
  pageSize: 25,
  totalCount: 0,
  totalPages: 0,
  hasPreviousPage: false,
  hasNextPage: false,
}

function getPositiveInteger(value: string | null, fallback: number): number {
  const parsed = Number(value)
  return Number.isInteger(parsed) && parsed > 0 ? parsed : fallback
}

function getViewState(locationSearch: string): UserListViewState {
  const params = new URLSearchParams(locationSearch)
  const role = params.get('role')
  const status = params.get('status')
  const pageSize = getPositiveInteger(params.get('pageSize'), 25)

  return {
    search: params.get('search') ?? '',
    role: isAdminUserRole(role) ? role : 'All',
    status: isUserAccountStatusFilter(status) ? status : 'All',
    page: getPositiveInteger(params.get('page'), 1),
    pageSize: [25, 50, 100].includes(pageSize) ? pageSize : 25,
  }
}

function createUserListSearch(viewState: UserListViewState): string {
  const params = new URLSearchParams()
  params.set('page', String(viewState.page))
  params.set('pageSize', String(viewState.pageSize))

  if (viewState.search.trim()) params.set('search', viewState.search.trim())
  if (viewState.role !== 'All') params.set('role', viewState.role)
  if (viewState.status !== 'All') params.set('status', viewState.status)

  return params.toString()
}

export function AdminUsersPage() {
  const location = useLocation()
  const navigate = useNavigate()
  const viewState = getViewState(location.search)
  const [users, setUsers] = useState<AdminUserListItem[]>([])
  const [pagination, setPagination] = useState<PaginationMetadata>(emptyPagination)
  const [loading, setLoading] = useState(true)
  const [hasLoaded, setHasLoaded] = useState(false)
  const [error, setError] = useState('')
  const [reloadKey, setReloadKey] = useState(0)
  const [searchInput, setSearchInput] = useState(viewState.search)
  const latestRequest = useRef(0)

  function updateViewState(changes: Partial<UserListViewState>, replace = false) {
    const nextViewState = { ...viewState, ...changes }
    navigate(
      {
        pathname: location.pathname,
        search: `?${createUserListSearch(nextViewState)}`,
      },
      { replace },
    )
  }

  useEffect(() => {
    setSearchInput(viewState.search)
  }, [viewState.search])

  useEffect(() => {
    if (searchInput === viewState.search) return

    const timeoutId = window.setTimeout(() => {
      const nextViewState = { ...getViewState(location.search), page: 1, search: searchInput }
      navigate(
        { pathname: location.pathname, search: `?${createUserListSearch(nextViewState)}` },
      )
    }, 375)

    return () => window.clearTimeout(timeoutId)
  }, [location.pathname, location.search, navigate, searchInput, viewState.search])

  useEffect(() => {
    const requestId = latestRequest.current + 1
    latestRequest.current = requestId

    async function loadUsers() {
      setLoading(true)
      setError('')

      try {
        const response = await getAdminUsers({
          search: viewState.search.trim() || undefined,
          role: viewState.role === 'All' ? undefined : viewState.role,
          status: viewState.status === 'All' ? undefined : viewState.status,
          page: viewState.page,
          pageSize: viewState.pageSize,
        })

        if (latestRequest.current !== requestId) return

        if (response.pagination.totalPages > 0 && viewState.page > response.pagination.totalPages) {
          const nextViewState = { ...getViewState(location.search), page: response.pagination.totalPages }
          navigate(
            { pathname: location.pathname, search: `?${createUserListSearch(nextViewState)}` },
            { replace: true },
          )
          return
        }

        setUsers(response.items)
        setPagination(response.pagination)
        setHasLoaded(true)
      } catch {
        if (latestRequest.current === requestId) {
          setError('Kullanıcılar yüklenemedi. Lütfen tekrar deneyin.')
        }
      } finally {
        if (latestRequest.current === requestId) {
          setLoading(false)
        }
      }
    }

    void loadUsers()
  }, [location.pathname, location.search, navigate, reloadKey, viewState.page, viewState.pageSize, viewState.role, viewState.search, viewState.status])

  const filtersActive = viewState.search.trim() !== '' || viewState.role !== 'All' || viewState.status !== 'All'

  function clearFilters() {
    setSearchInput('')
    updateViewState({ page: 1, role: 'All', search: '', status: 'All' })
  }

  function openUser(userId: number) {
    navigate(`/admin/users/${userId}`)
  }

  function handleUserKeyDown(event: KeyboardEvent<HTMLElement>, userId: number) {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault()
      openUser(userId)
    }
  }

  return (
    <Stack spacing={3}>
      <PageHeader
        description="Kullanıcı hesaplarını arayın, filtreleyin ve erişim durumlarını güvenli biçimde yönetin."
        eyebrow="Yönetim"
        title="Kullanıcılar"
      />

      <Paper sx={{ border: '1px solid', borderColor: 'divider', p: 2 }}>
        <Stack spacing={1.5}>
          <Box
            sx={{
              display: 'grid',
              gap: 1.5,
              gridTemplateColumns: { xs: '1fr', md: 'minmax(260px, 2fr) repeat(2, minmax(170px, 1fr)) auto' },
            }}
          >
            <TextField
              label="Ara"
              onChange={(event) => setSearchInput(event.target.value)}
              placeholder="Ad veya e-posta ara"
              slotProps={{
                input: {
                  startAdornment: (
                    <InputAdornment position="start">
                      <Search aria-hidden="true" />
                    </InputAdornment>
                  ),
                },
              }}
              value={searchInput}
            />
            <FormControl>
              <InputLabel id="user-role-filter-label">Rol</InputLabel>
              <Select
                label="Rol"
                labelId="user-role-filter-label"
                onChange={(event: SelectChangeEvent) =>
                  updateViewState({ page: 1, role: event.target.value as RoleFilter })
                }
                value={viewState.role}
              >
                <MenuItem value="All">Tüm Roller</MenuItem>
                {Object.values(UserRole).map((role) => (
                  <MenuItem key={role} value={role}>{getUserRoleLabel(role)}</MenuItem>
                ))}
              </Select>
            </FormControl>
            <FormControl>
              <InputLabel id="user-status-filter-label">Durum</InputLabel>
              <Select
                label="Durum"
                labelId="user-status-filter-label"
                onChange={(event: SelectChangeEvent) =>
                  updateViewState({ page: 1, status: event.target.value as StatusFilter })
                }
                value={viewState.status}
              >
                <MenuItem value="All">Tüm Durumlar</MenuItem>
                <MenuItem value={UserAccountStatusFilter.Active}>Aktif</MenuItem>
                <MenuItem value={UserAccountStatusFilter.Inactive}>Pasif</MenuItem>
              </Select>
            </FormControl>
            {filtersActive ? <Button onClick={clearFilters} variant="outlined">Filtreleri Temizle</Button> : <Box />}
          </Box>
          <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
            <FilterAltOutlined color="action" fontSize="small" />
            <Typography color="text.secondary" variant="body2">{pagination.totalCount} kullanıcı</Typography>
          </Stack>
        </Stack>
      </Paper>

      {loading && hasLoaded ? <LinearProgress aria-label="Kullanıcılar güncelleniyor" /> : null}

      {loading && !hasLoaded ? (
        <Box sx={{ display: 'grid', minHeight: 320, placeItems: 'center' }}>
          <CircularProgress aria-label="Kullanıcılar yükleniyor" />
        </Box>
      ) : error ? (
        <Alert action={<Button color="inherit" onClick={() => setReloadKey((key) => key + 1)}>Tekrar Dene</Button>} severity="error">
          {error}
        </Alert>
      ) : users.length === 0 ? (
        <EmptyStateCard
          description={filtersActive ? 'Arama ve filtrelerle eşleşen bir kullanıcı bulunamadı.' : 'Görüntülenecek kullanıcı hesabı bulunmuyor.'}
          icon={<Search />}
          title="Kullanıcı bulunamadı"
        />
      ) : (
        <>
          <TableContainer component={Paper} sx={{ border: '1px solid', borderColor: 'divider', display: { xs: 'none', md: 'block' }, overflowX: 'auto' }}>
            <Table aria-label="Kullanıcı hesapları">
              <TableHead>
                <TableRow>
                  <TableCell>Ad Soyad</TableCell>
                  <TableCell>E-posta</TableCell>
                  <TableCell>Rol</TableCell>
                  <TableCell>Durum</TableCell>
                  <TableCell align="right">Aktif Talep</TableCell>
                  <TableCell>Oluşturulma</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {users.map((user) => (
                  <TableRow
                    hover
                    key={user.id}
                    onClick={() => openUser(user.id)}
                    onKeyDown={(event) => handleUserKeyDown(event, user.id)}
                    role="link"
                    sx={{ cursor: 'pointer', '&:focus-visible': { outline: '3px solid', outlineColor: 'primary.light' } }}
                    tabIndex={0}
                  >
                    <TableCell sx={{ fontWeight: 700 }}>{user.fullName}</TableCell>
                    <TableCell>{user.email}</TableCell>
                    <TableCell><UserRoleChip role={user.role} /></TableCell>
                    <TableCell><UserAccountStatusChip isActive={user.isActive} /></TableCell>
                    <TableCell align="right">{user.activeTicketCount}</TableCell>
                    <TableCell>{formatDate(user.createdAt)}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>

          <Stack spacing={1.5} sx={{ display: { xs: 'flex', md: 'none' } }}>
            {users.map((user) => (
              <Paper
                key={user.id}
                onClick={() => openUser(user.id)}
                onKeyDown={(event) => handleUserKeyDown(event, user.id)}
                role="link"
                sx={{ border: '1px solid', borderColor: 'divider', cursor: 'pointer', p: 2, '&:focus-visible': { outline: '3px solid', outlineColor: 'primary.light' } }}
                tabIndex={0}
              >
                <Stack spacing={1.1}>
                  <Box>
                    <Typography sx={{ fontWeight: 800 }} variant="body1">{user.fullName}</Typography>
                    <Typography color="text.secondary" variant="body2">{user.email}</Typography>
                  </Box>
                  <Stack direction="row" spacing={0.8} sx={{ flexWrap: 'wrap' }}>
                    <UserRoleChip role={user.role} />
                    <UserAccountStatusChip isActive={user.isActive} />
                  </Stack>
                  <Typography color="text.secondary" variant="body2">
                    Aktif talep: {user.activeTicketCount} · Oluşturulma: {formatDate(user.createdAt)}
                  </Typography>
                </Stack>
              </Paper>
            ))}
          </Stack>

          <PaginationControls
            itemLabel="kullanıcı"
            onPageChange={(page) => updateViewState({ page })}
            onPageSizeChange={(pageSize) => updateViewState({ page: 1, pageSize })}
            pagination={pagination}
          />
        </>
      )}
    </Stack>
  )
}
