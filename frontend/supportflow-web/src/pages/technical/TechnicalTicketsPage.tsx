import { FilterAltOutlined, Search } from '@mui/icons-material'
import {
  Alert,
  Box,
  Button,
  Chip,
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
import { QueueAgingChip } from '../../components/common/QueueAgingChip'
import { RequesterTypeChip } from '../../components/common/RequesterTypeChip'
import { TicketPriorityChip } from '../../components/common/TicketPriorityChip'
import { TicketCategoryChip } from '../../components/common/TicketCategoryChip'
import { TicketTagChip } from '../../components/common/TicketTagChip'
import { SlaStateChip } from '../../components/common/SlaStateChip'
import { TicketPagination } from '../../components/common/TicketPagination'
import { TicketStatusChip } from '../../components/common/TicketStatusChip'
import { getTickets } from '../../services/ticketService'
import { getAssignableCategories, getAssignableTags } from '../../services/taxonomyService'
import { getTechnicalStaff } from '../../services/userService'
import {
  TicketAssignmentFilter,
  TicketAgingFilter,
  TicketListSort,
  TicketPriority,
  TicketStatus,
  type TicketListItem,
} from '../../types/ticket'
import type { TicketClassificationItem } from '../../types/taxonomy'
import type { PaginationMetadata } from '../../types/pagination'
import type { TechnicalStaffUser } from '../../types/user'
import { formatDate } from '../../utils/formatDate'
import { formatDuration } from '../../utils/formatDuration'
import { getTicketPriorityLabel, getTicketStatusLabel } from '../../utils/ticketLabels'

type StatusFilter = TicketStatus | 'All'
type PriorityFilter = TicketPriority | 'All'
type AgingFilter = TicketAgingFilter | 'All'
type SortOption = TicketListSort | 'Default'
type AssignmentFilter = TicketAssignmentFilter | 'All' | `User:${number}`
type ClassificationFilter = 'All' | 'Unclassified'

interface TicketListViewState {
  search: string
  statusFilter: StatusFilter
  priorityFilter: PriorityFilter
  agingFilter: AgingFilter
  sort: SortOption
  assignmentFilter: AssignmentFilter
  activeOnly: boolean
  categoryId: number | null
  tagId: number | null
  classificationState: ClassificationFilter
  page: number
  pageSize: number
}

function isEnumValue<T extends Record<string, string>>(
  values: T,
  value: string | null,
): value is T[keyof T] {
  return value !== null && Object.values(values).includes(value as T[keyof T])
}

function getPositiveInteger(value: string | null, fallback: number): number {
  const parsed = Number(value)
  return Number.isInteger(parsed) && parsed > 0 ? parsed : fallback
}

function getViewState(locationSearch: string): TicketListViewState {
  const fallback: TicketListViewState = {
    search: '',
    statusFilter: 'All',
    priorityFilter: 'All',
    agingFilter: 'All',
    sort: 'Default',
    assignmentFilter: 'All',
    activeOnly: false,
    categoryId: null,
    tagId: null,
    classificationState: 'All',
    page: 1,
    pageSize: 25,
  }

  const params = new URLSearchParams(locationSearch)
  const assignedUserId = Number(params.get('assignedUserId'))
  const assignmentState = params.get('assignmentState')
  const status = params.get('status')
  const priority = params.get('priority')
  const agingFilter = params.get('agingFilter')
  const sort = params.get('sort')
  const categoryId = Number(params.get('categoryId'))
  const tagId = Number(params.get('tagId'))
  const classificationState = params.get('classificationState')
  const pageSize = getPositiveInteger(params.get('pageSize'), fallback.pageSize)

  return {
    search: params.get('search') ?? fallback.search,
    statusFilter: isEnumValue(TicketStatus, status)
      ? status
      : fallback.statusFilter,
    priorityFilter: isEnumValue(TicketPriority, priority)
      ? priority
      : fallback.priorityFilter,
    agingFilter: isEnumValue(TicketAgingFilter, agingFilter)
      ? agingFilter
      : fallback.agingFilter,
    sort: isEnumValue(TicketListSort, sort) ? sort : fallback.sort,
    assignmentFilter: Number.isInteger(assignedUserId) && assignedUserId > 0
      ? `User:${assignedUserId}`
      : isEnumValue(TicketAssignmentFilter, assignmentState)
        ? assignmentState
        : fallback.assignmentFilter,
    activeOnly: params.get('activeOnly') === 'true',
    categoryId: Number.isInteger(categoryId) && categoryId > 0 ? categoryId : null,
    tagId: Number.isInteger(tagId) && tagId > 0 ? tagId : null,
    classificationState: classificationState === 'Unclassified' && !(Number.isInteger(categoryId) && categoryId > 0)
      ? 'Unclassified'
      : 'All',
    page: getPositiveInteger(params.get('page'), fallback.page),
    pageSize: [25, 50, 100].includes(pageSize) ? pageSize : fallback.pageSize,
  }
}

function getAssignedUserId(assignmentFilter: AssignmentFilter): number | undefined {
  return assignmentFilter.startsWith('User:')
    ? Number(assignmentFilter.slice('User:'.length))
    : undefined
}

function getAssignmentState(
  assignmentFilter: AssignmentFilter,
): TicketAssignmentFilter | undefined {
  return assignmentFilter === TicketAssignmentFilter.Unassigned
    || assignmentFilter === TicketAssignmentFilter.InactiveAssignee
    ? assignmentFilter
    : undefined
}

function createTicketListSearch(viewState: TicketListViewState): string {
  const params = new URLSearchParams()
  params.set('page', String(viewState.page))
  params.set('pageSize', String(viewState.pageSize))

  if (viewState.search.trim()) params.set('search', viewState.search.trim())
  if (viewState.statusFilter !== 'All') params.set('status', viewState.statusFilter)
  if (viewState.priorityFilter !== 'All') params.set('priority', viewState.priorityFilter)
  if (viewState.agingFilter !== 'All') params.set('agingFilter', viewState.agingFilter)
  if (viewState.sort !== 'Default') params.set('sort', viewState.sort)

  const assignedUserId = getAssignedUserId(viewState.assignmentFilter)
  const assignmentState = getAssignmentState(viewState.assignmentFilter)
  if (assignedUserId) {
    params.set('assignedUserId', String(assignedUserId))
  } else if (assignmentState) {
    params.set('assignmentState', assignmentState)
  }
  if (viewState.activeOnly) params.set('activeOnly', 'true')
  if (viewState.categoryId) params.set('categoryId', String(viewState.categoryId))
  if (viewState.tagId) params.set('tagId', String(viewState.tagId))
  if (viewState.classificationState === 'Unclassified') params.set('classificationState', 'Unclassified')

  return params.toString()
}

export function TechnicalTicketsPage() {
  const location = useLocation()
  const navigate = useNavigate()
  const routeBase = location.pathname.startsWith('/admin') ? '/admin' : '/technical'
  const viewState = getViewState(location.search)
  const [tickets, setTickets] = useState<TicketListItem[]>([])
  const [technicalStaff, setTechnicalStaff] = useState<TechnicalStaffUser[]>([])
  const [categoryOptions, setCategoryOptions] = useState<TicketClassificationItem[]>([])
  const [tagOptions, setTagOptions] = useState<TicketClassificationItem[]>([])
  const [loading, setLoading] = useState(true)
  const [hasLoaded, setHasLoaded] = useState(false)
  const [error, setError] = useState('')
  const [reloadKey, setReloadKey] = useState(0)
  const [pagination, setPagination] = useState<PaginationMetadata>({
    page: 1,
    pageSize: 25,
    totalCount: 0,
    totalPages: 0,
    hasPreviousPage: false,
    hasNextPage: false,
  })
  const [searchInput, setSearchInput] = useState(viewState.search)
  const latestRequest = useRef(0)
  const taxonomyRequest = useRef(0)

  function updateViewState(
    changes: Partial<TicketListViewState>,
    options?: { replace?: boolean },
  ) {
    const nextViewState = { ...viewState, ...changes }
    navigate(
      {
        pathname: location.pathname,
        search: `?${createTicketListSearch(nextViewState)}`,
      },
      { replace: options?.replace ?? false },
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
        { pathname: location.pathname, search: `?${createTicketListSearch(nextViewState)}` },
      )
    }, 375)

    return () => window.clearTimeout(timeoutId)
  }, [location.pathname, location.search, navigate, searchInput, viewState.search])

  useEffect(() => {
    if (routeBase !== '/admin') {
      return
    }

    let active = true
    void getTechnicalStaff().then((users) => {
      if (active) {
        setTechnicalStaff(users)
      }
    }).catch(() => {
      if (active) {
        setTechnicalStaff([])
      }
    })

    return () => {
      active = false
    }
  }, [routeBase])

  useEffect(() => {
    const requestId = taxonomyRequest.current + 1
    taxonomyRequest.current = requestId
    let active = true
    Promise.all([getAssignableCategories(false), getAssignableTags(false)])
      .then(([categories, tags]) => {
        if (!active || taxonomyRequest.current !== requestId) return
        setCategoryOptions(categories)
        setTagOptions(tags)
      })
      .catch(() => {
        if (!active || taxonomyRequest.current !== requestId) return
        setCategoryOptions([])
        setTagOptions([])
      })
    return () => { active = false }
  }, [])

  useEffect(() => {
    const requestId = latestRequest.current + 1
    latestRequest.current = requestId

    async function loadTickets() {
      setLoading(true)
      setError('')
      try {
        const response = await getTickets({
          priority: viewState.priorityFilter === 'All' ? undefined : viewState.priorityFilter,
          status: viewState.statusFilter === 'All' ? undefined : viewState.statusFilter,
          search: viewState.search.trim() || undefined,
          assignedUserId: getAssignedUserId(viewState.assignmentFilter),
          assignmentState: getAssignmentState(viewState.assignmentFilter),
          activeOnly: viewState.activeOnly || undefined,
          agingFilter: viewState.agingFilter === 'All' ? undefined : viewState.agingFilter,
          sort: viewState.sort === 'Default' ? undefined : viewState.sort,
          categoryId: viewState.categoryId ?? undefined,
          classificationState: viewState.classificationState === 'All' ? undefined : viewState.classificationState,
          tagId: viewState.tagId ?? undefined,
          page: viewState.page,
          pageSize: viewState.pageSize,
        })
        if (latestRequest.current !== requestId) return
        if (response.pagination.totalPages > 0 && viewState.page > response.pagination.totalPages) {
          const nextViewState = {
            ...getViewState(location.search),
            page: response.pagination.totalPages,
          }
          navigate(
            { pathname: location.pathname, search: `?${createTicketListSearch(nextViewState)}` },
            { replace: true },
          )
          return
        }
        setTickets(response.items)
        setPagination(response.pagination)
        setHasLoaded(true)
      } catch {
        if (latestRequest.current === requestId) {
          setError('Talepler yüklenemedi. Lütfen tekrar deneyin.')
        }
      } finally {
        if (latestRequest.current === requestId) {
          setLoading(false)
        }
      }
    }

    void loadTickets()
  }, [location.pathname, location.search, navigate, reloadKey, viewState.activeOnly, viewState.agingFilter, viewState.assignmentFilter, viewState.categoryId, viewState.classificationState, viewState.page, viewState.pageSize, viewState.priorityFilter, viewState.search, viewState.sort, viewState.statusFilter, viewState.tagId])

  const filtersActive =
    viewState.search.trim() !== '' ||
    viewState.statusFilter !== 'All' ||
    viewState.priorityFilter !== 'All' ||
    viewState.agingFilter !== 'All' ||
    viewState.sort !== 'Default' ||
    viewState.assignmentFilter !== 'All' ||
    viewState.activeOnly ||
    viewState.categoryId !== null ||
    viewState.tagId !== null ||
    viewState.classificationState !== 'All'

  const filteredTickets = tickets

  function clearFilters() {
    setSearchInput('')
    updateViewState({
      search: '',
      statusFilter: 'All',
      priorityFilter: 'All',
      agingFilter: 'All',
      sort: 'Default',
      assignmentFilter: 'All',
      activeOnly: false,
      categoryId: null,
      tagId: null,
      classificationState: 'All',
      page: 1,
    })
  }

  function openTicket(ticketId: number) {
    navigate(`${routeBase}/tickets/${ticketId}`)
  }

  function handleTicketKeyDown(event: KeyboardEvent<HTMLElement>, ticketId: number) {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault()
      openTicket(ticketId)
    }
  }

  return (
    <Stack spacing={3}>
      <PageHeader
        description="Talepleri arayın, filtreleyin ve sıralayın. Masaüstünde kompakt operasyon tablosu, mobilde kart görünümü kullanılır."
        eyebrow={routeBase === '/admin' ? 'Yönetim Görünümü' : 'Teknik Operasyon'}
        title="Talepler"
      />

      <Paper sx={{ border: '1px solid', borderColor: 'divider', p: 2 }}>
        <Stack spacing={1.5}>
          <Box
            sx={{
              display: 'grid',
              gap: 1.5,
              gridTemplateColumns: {
                xs: '1fr',
                sm: 'repeat(2, minmax(0, 1fr))',
                lg: routeBase === '/admin'
                  ? 'minmax(240px, 2fr) repeat(5, minmax(140px, 1fr)) auto'
                  : 'minmax(240px, 2fr) repeat(4, minmax(140px, 1fr)) auto',
              },
            }}
          >
            <TextField
              label="Ara"
              onChange={(event) => setSearchInput(event.target.value)}
              placeholder="Talep no, konu veya müşteri"
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
              <InputLabel id="status-filter-label">Durum</InputLabel>
              <Select
                label="Durum"
                labelId="status-filter-label"
                onChange={(event: SelectChangeEvent) =>
                  updateViewState({ page: 1, statusFilter: event.target.value as StatusFilter })
                }
                value={viewState.statusFilter}
              >
                <MenuItem value="All">Tüm Durumlar</MenuItem>
                {Object.values(TicketStatus).map((status) => (
                  <MenuItem key={status} value={status}>
                    {getTicketStatusLabel(status)}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
            <FormControl>
              <InputLabel id="priority-filter-label">Öncelik</InputLabel>
              <Select
                label="Öncelik"
                labelId="priority-filter-label"
                onChange={(event: SelectChangeEvent) =>
                  updateViewState({ page: 1, priorityFilter: event.target.value as PriorityFilter })
                }
                value={viewState.priorityFilter}
              >
                <MenuItem value="All">Tüm Öncelikler</MenuItem>
                {Object.values(TicketPriority).map((priority) => (
                  <MenuItem key={priority} value={priority}>
                    {getTicketPriorityLabel(priority)}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
            {routeBase === '/admin' ? (
              <FormControl>
                <InputLabel id="assignment-filter-label">Atama</InputLabel>
                <Select
                  label="Atama"
                  labelId="assignment-filter-label"
                  onChange={(event: SelectChangeEvent) =>
                  updateViewState({ page: 1, assignmentFilter: event.target.value as AssignmentFilter })
                  }
                  value={viewState.assignmentFilter}
                >
                  <MenuItem value="All">Tümü</MenuItem>
                  <MenuItem value={TicketAssignmentFilter.Unassigned}>Atanmamış</MenuItem>
                  <MenuItem value={TicketAssignmentFilter.InactiveAssignee}>
                    Pasif Personele Atanmış
                  </MenuItem>
                  {technicalStaff.map((staff) => (
                    <MenuItem key={staff.id} value={`User:${staff.id}`}>
                      {staff.firstName} {staff.lastName}
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>
            ) : null}
            <FormControl>
              <InputLabel id="aging-filter-label">Bekleme</InputLabel>
              <Select
                label="Bekleme"
                labelId="aging-filter-label"
                onChange={(event: SelectChangeEvent) =>
                  updateViewState({ page: 1, agingFilter: event.target.value as AgingFilter })
                }
                value={viewState.agingFilter}
              >
                <MenuItem value="All">Tüm Bekleme Durumları</MenuItem>
                <MenuItem value={TicketAgingFilter.Awaiting}>Yanıt Bekleyenler</MenuItem>
                <MenuItem value={TicketAgingFilter.WaitingOver2Hours}>2+ Saat Bekleyenler</MenuItem>
                <MenuItem value={TicketAgingFilter.WaitingOver8Hours}>8+ Saat Bekleyenler</MenuItem>
                <MenuItem value={TicketAgingFilter.WaitingOver24Hours}>24+ Saat Bekleyenler</MenuItem>
              </Select>
            </FormControl>
            <FormControl>
              <InputLabel id="sort-label">Sırala</InputLabel>
              <Select
                label="Sırala"
                labelId="sort-label"
                onChange={(event: SelectChangeEvent) =>
                  updateViewState({ page: 1, sort: event.target.value as SortOption })
                }
                value={viewState.sort}
              >
                <MenuItem value="Default">Varsayılan</MenuItem>
                <MenuItem value={TicketListSort.LongestWaiting}>En Uzun Bekleyen</MenuItem>
                <MenuItem value={TicketListSort.ShortestWaiting}>En Kısa Bekleyen</MenuItem>
                <MenuItem value={TicketListSort.OldestTicket}>En Eski Talep</MenuItem>
                <MenuItem value={TicketListSort.NewestTicket}>En Yeni Talep</MenuItem>
              </Select>
            </FormControl>
            {filtersActive ? (
              <Button onClick={clearFilters} variant="outlined">
                Filtreleri Temizle
              </Button>
            ) : (
              <Box />
            )}
          </Box>

          <Box sx={{ display: 'grid', gap: 1.5, gridTemplateColumns: { xs: '1fr', sm: 'repeat(2, minmax(0, 1fr))' } }}>
            <FormControl>
              <InputLabel id="category-filter-label">Kategori</InputLabel>
              <Select
                label="Kategori"
                labelId="category-filter-label"
                onChange={(event: SelectChangeEvent) => {
                  const value = event.target.value
                  updateViewState({
                    page: 1,
                    categoryId: value === 'All' || value === 'Unclassified' ? null : Number(value),
                    classificationState: value === 'Unclassified' ? 'Unclassified' : 'All',
                  })
                }}
                value={viewState.classificationState === 'Unclassified' ? 'Unclassified' : viewState.categoryId?.toString() ?? 'All'}
              >
                <MenuItem value="All">Tümü</MenuItem>
                <MenuItem value="Unclassified">Sınıflandırılmamış</MenuItem>
                {categoryOptions.filter((category) => category.isActive || category.id === viewState.categoryId).map((category) => <MenuItem key={category.id} value={category.id}>{category.name}{category.isActive ? '' : ' (Pasif)'}</MenuItem>)}
              </Select>
            </FormControl>
            <FormControl>
              <InputLabel id="tag-filter-label">Etiket</InputLabel>
              <Select label="Etiket" labelId="tag-filter-label" onChange={(event: SelectChangeEvent) => updateViewState({ page: 1, tagId: event.target.value === 'All' ? null : Number(event.target.value) })} value={viewState.tagId?.toString() ?? 'All'}>
                <MenuItem value="All">Tümü</MenuItem>
                {tagOptions.filter((tag) => tag.isActive || tag.id === viewState.tagId).map((tag) => <MenuItem key={tag.id} value={tag.id}>{tag.name}{tag.isActive ? '' : ' (Pasif)'}</MenuItem>)}
              </Select>
            </FormControl>
          </Box>

          <Stack direction="row" spacing={1} sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
            <FilterAltOutlined color="action" fontSize="small" />
            <Typography color="text.secondary" variant="body2">
              {pagination.totalCount} talep
            </Typography>
            {viewState.activeOnly ? (
              <Chip
                label="Yalnızca aktif talepler"
                onDelete={() => updateViewState({ activeOnly: false, page: 1 })}
                size="small"
                variant="outlined"
              />
            ) : null}
          </Stack>
        </Stack>
      </Paper>

      {loading && hasLoaded ? <LinearProgress aria-label="Talepler güncelleniyor" /> : null}

      {error && hasLoaded ? (
        <Alert
          action={
            <Button color="inherit" onClick={() => setReloadKey((key) => key + 1)}>
              Tekrar Dene
            </Button>
          }
          severity="error"
        >
          {error}
        </Alert>
      ) : null}

      {loading && !hasLoaded ? (
        <Box sx={{ display: 'grid', minHeight: 320, placeItems: 'center' }}>
          <CircularProgress aria-label="Talepler yükleniyor" />
        </Box>
      ) : error && !hasLoaded ? (
        <Alert
          action={
            <Button color="inherit" onClick={() => setReloadKey((key) => key + 1)}>
              Tekrar Dene
            </Button>
          }
          severity="error"
        >
          {error}
        </Alert>
      ) : filteredTickets.length === 0 ? (
        <EmptyStateCard
          description={
            tickets.length === 0
              ? 'Sistemde görüntülenecek bir destek talebi bulunmuyor.'
              : 'Arama ve filtrelerle eşleşen bir kayıt bulunamadı.'
          }
          icon={<Search />}
          title="Kayıt bulunamadı"
        />
      ) : (
        <>
          <TableContainer
            component={Paper}
            sx={{
              border: '1px solid',
              borderColor: 'divider',
              display: { xs: 'none', md: 'block' },
              overflowX: 'auto',
            }}
          >
            <Table aria-label="Tüm destek talepleri">
              <TableHead>
                <TableRow>
                  <TableCell>Talep</TableCell>
                  <TableCell>Konu</TableCell>
                  <TableCell>Müşteri</TableCell>
                  <TableCell>Durum</TableCell>
                  <TableCell>Öncelik</TableCell>
                  <TableCell>Kategori</TableCell>
                  <TableCell>SLA</TableCell>
                  <TableCell>Bekleme</TableCell>
                  <TableCell>Atanan</TableCell>
                  <TableCell>Son Güncelleme</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {filteredTickets.map((ticket) => (
                  <TableRow
                    hover
                    key={ticket.id}
                    onClick={() => openTicket(ticket.id)}
                    onKeyDown={(event) => handleTicketKeyDown(event, ticket.id)}
                    role="link"
                    sx={{
                      cursor: 'pointer',
                      '&:hover': {
                        bgcolor: 'rgba(148, 163, 184, 0.08)',
                      },
                    }}
                    tabIndex={0}
                  >
                    <TableCell sx={{ fontWeight: 700 }}>{ticket.ticketNumber}</TableCell>
                    <TableCell>{ticket.title}</TableCell>
                    <TableCell>
                      <Stack spacing={0.25}>
                        <Stack direction="row" spacing={0.6} sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
                          <Typography variant="body2">{ticket.customerName}</Typography>
                          <RequesterTypeChip requesterType={ticket.requesterType} />
                        </Stack>
                        <Typography color="text.secondary" variant="caption">{ticket.requesterEmail}</Typography>
                        <Typography color="text.secondary" variant="caption">
                          {formatDate(ticket.createdAt)}
                        </Typography>
                      </Stack>
                    </TableCell>
                    <TableCell>
                      <TicketStatusChip status={ticket.status} />
                    </TableCell>
                    <TableCell>
                      <TicketPriorityChip priority={ticket.priority} />
                    </TableCell>
                    <TableCell>
                      <Stack spacing={0.55}>
                        <TicketCategoryChip category={ticket.category} />
                        {ticket.tags.length > 0 ? (
                          <Stack direction="row" spacing={0.4} sx={{ flexWrap: 'wrap' }}>
                            {ticket.tags.slice(0, 2).map((tag) => <TicketTagChip key={tag.id} tag={tag} />)}
                            {ticket.tags.length > 2 ? <Chip label={`+${ticket.tags.length - 2}`} size="small" variant="outlined" /> : null}
                          </Stack>
                        ) : null}
                      </Stack>
                    </TableCell>
                    <TableCell>
                      <SlaStateChip state={ticket.sla.mostUrgentState} />
                    </TableCell>
                    <TableCell>
                      {ticket.isAwaitingStaffReply && ticket.awaitingStaffMinutes !== null ? (
                        <Stack spacing={0.55}>
                          <Typography sx={{ fontWeight: 700 }} variant="body2">
                            {formatDuration(ticket.awaitingStaffMinutes)}
                          </Typography>
                          {ticket.agingBucket ? <QueueAgingChip bucket={ticket.agingBucket} /> : null}
                        </Stack>
                      ) : (
                        <Typography color="text.secondary" variant="body2">-</Typography>
                      )}
                    </TableCell>
                    <TableCell>{ticket.assignedUserName || 'Atanmadı'}</TableCell>
                    <TableCell>{formatDate(ticket.updatedAt)}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>

          <Stack spacing={2} sx={{ display: { xs: 'flex', md: 'none' } }}>
            {filteredTickets.map((ticket) => (
              <Paper
                key={ticket.id}
                onClick={() => openTicket(ticket.id)}
                onKeyDown={(event) => handleTicketKeyDown(event, ticket.id)}
                role="link"
                sx={{
                  border: '1px solid',
                  borderColor: 'divider',
                  cursor: 'pointer',
                  p: 2.25,
                  '&:focus-visible': {
                    outline: '3px solid',
                    outlineColor: 'primary.light',
                  },
                }}
                tabIndex={0}
              >
                <Typography color="text.secondary" variant="caption">
                  {ticket.ticketNumber} · {ticket.customerName}
                </Typography>
                <Typography sx={{ fontWeight: 700, mt: 0.5 }} variant="body2">
                  {ticket.title}
                </Typography>
                <Stack direction="row" spacing={0.8} sx={{ flexWrap: 'wrap', mt: 1.2 }}>
                  <TicketStatusChip status={ticket.status} />
                  <TicketPriorityChip priority={ticket.priority} />
                  <TicketCategoryChip category={ticket.category} />
                  {ticket.tags.slice(0, 2).map((tag) => <TicketTagChip key={tag.id} tag={tag} />)}
                  {ticket.tags.length > 2 ? <Chip label={`+${ticket.tags.length - 2}`} size="small" variant="outlined" /> : null}
                  <SlaStateChip state={ticket.sla.mostUrgentState} />
                  {ticket.isAwaitingStaffReply && ticket.agingBucket ? (
                    <QueueAgingChip bucket={ticket.agingBucket} />
                  ) : null}
                </Stack>
                {ticket.isAwaitingStaffReply && ticket.awaitingStaffMinutes !== null ? (
                  <Typography color="text.secondary" sx={{ mt: 1.2 }} variant="caption">
                    Bekleme: {formatDuration(ticket.awaitingStaffMinutes)} · Yanıt bekliyor
                  </Typography>
                ) : null}
                <Typography color="text.secondary" sx={{ mt: 1.2 }} variant="caption">
                  Atanan: {ticket.assignedUserName || 'Atanmadı'}
                </Typography>
                <Typography color="text.secondary" variant="caption">
                  Son güncelleme: {formatDate(ticket.updatedAt)}
                </Typography>
              </Paper>
            ))}
          </Stack>
          <TicketPagination
            onPageChange={(page) => updateViewState({ page })}
            onPageSizeChange={(pageSize) => updateViewState({ page: 1, pageSize })}
            pagination={pagination}
          />
        </>
      )}
    </Stack>
  )
}
