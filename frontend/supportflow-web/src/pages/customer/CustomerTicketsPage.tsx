import { Add, FilterAltOutlined, Search } from '@mui/icons-material'
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
import { TicketPagination } from '../../components/common/TicketPagination'
import { TicketPriorityChip } from '../../components/common/TicketPriorityChip'
import { TicketCategoryChip } from '../../components/common/TicketCategoryChip'
import { TicketStatusChip } from '../../components/common/TicketStatusChip'
import { getTickets } from '../../services/ticketService'
import { TicketListSort, TicketPriority, TicketStatus, type TicketListItem } from '../../types/ticket'
import type { PaginationMetadata } from '../../types/pagination'
import { formatDate } from '../../utils/formatDate'
import { getTicketPriorityLabel, getTicketStatusLabel } from '../../utils/ticketLabels'

type StatusFilter = TicketStatus | 'All'
type PriorityFilter = TicketPriority | 'All'
type SortOption =
  | typeof TicketListSort.UpdatedNewest
  | typeof TicketListSort.NewestTicket
  | typeof TicketListSort.OldestTicket

interface CustomerTicketListViewState {
  search: string
  statusFilter: StatusFilter
  priorityFilter: PriorityFilter
  sort: SortOption
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

function getViewState(locationSearch: string): CustomerTicketListViewState {
  const params = new URLSearchParams(locationSearch)
  const status = params.get('status')
  const priority = params.get('priority')
  const sort = params.get('sort')
  const pageSize = getPositiveInteger(params.get('pageSize'), 25)

  return {
    search: params.get('search') ?? '',
    statusFilter: isEnumValue(TicketStatus, status) ? status : 'All',
    priorityFilter: isEnumValue(TicketPriority, priority) ? priority : 'All',
    sort: isEnumValue(TicketListSort, sort)
      && [TicketListSort.UpdatedNewest, TicketListSort.NewestTicket, TicketListSort.OldestTicket]
        .includes(sort as SortOption)
      ? sort as SortOption
      : TicketListSort.UpdatedNewest,
    page: getPositiveInteger(params.get('page'), 1),
    pageSize: [25, 50, 100].includes(pageSize) ? pageSize : 25,
  }
}

function createTicketListSearch(viewState: CustomerTicketListViewState): string {
  const params = new URLSearchParams()
  params.set('page', String(viewState.page))
  params.set('pageSize', String(viewState.pageSize))
  if (viewState.search.trim()) params.set('search', viewState.search.trim())
  if (viewState.statusFilter !== 'All') params.set('status', viewState.statusFilter)
  if (viewState.priorityFilter !== 'All') params.set('priority', viewState.priorityFilter)
  if (viewState.sort !== TicketListSort.UpdatedNewest) params.set('sort', viewState.sort)
  return params.toString()
}

export function CustomerTicketsPage() {
  const navigate = useNavigate()
  const location = useLocation()
  const viewState = getViewState(location.search)
  const [tickets, setTickets] = useState<TicketListItem[]>([])
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

  function updateViewState(
    changes: Partial<CustomerTicketListViewState>,
    options?: { replace?: boolean },
  ) {
    const nextViewState = { ...viewState, ...changes }
    navigate(
      { pathname: location.pathname, search: `?${createTicketListSearch(nextViewState)}` },
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
    const requestId = latestRequest.current + 1
    latestRequest.current = requestId

    async function loadTickets() {
      setLoading(true)
      setError('')

      try {
        const response = await getTickets({
          search: viewState.search.trim() || undefined,
          status: viewState.statusFilter === 'All' ? undefined : viewState.statusFilter,
          priority: viewState.priorityFilter === 'All' ? undefined : viewState.priorityFilter,
          sort: viewState.sort,
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
          setError('Destek talepleriniz yüklenemedi. Lütfen tekrar deneyin.')
        }
      } finally {
        if (latestRequest.current === requestId) {
          setLoading(false)
        }
      }
    }

    void loadTickets()
  }, [location.pathname, location.search, navigate, reloadKey, viewState.page, viewState.pageSize, viewState.priorityFilter, viewState.search, viewState.sort, viewState.statusFilter])

  const filtersActive =
    viewState.search.trim() !== '' ||
    viewState.statusFilter !== 'All' ||
    viewState.priorityFilter !== 'All' ||
    viewState.sort !== TicketListSort.UpdatedNewest

  const filteredTickets = tickets

  function clearFilters() {
    setSearchInput('')
    updateViewState({
      search: '',
      statusFilter: 'All',
      priorityFilter: 'All',
      sort: TicketListSort.UpdatedNewest,
      page: 1,
    })
  }

  function openTicket(ticketId: number) {
    navigate(`/customer/tickets/${ticketId}`)
  }

  function handleRowKeyDown(event: KeyboardEvent<HTMLElement>, ticketId: number) {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault()
      openTicket(ticketId)
    }
  }

  return (
    <Stack spacing={3}>
      <PageHeader
        actions={
          <Button
            fullWidth
            onClick={() => navigate('/customer/tickets/new')}
            startIcon={<Add />}
            sx={{ width: { xs: '100%', md: 'auto' } }}
            variant="contained"
          >
            Yeni Talep
          </Button>
        }
        description="Kayıtlarınızı arayın, filtreleyin ve en güncel destek konuşmalarına hızlıca ulaşın."
        eyebrow="Talep Merkezi"
        title="Taleplerim"
      />

      <Paper sx={{ border: '1px solid', borderColor: 'divider', p: 2 }}>
        <Stack spacing={1.5}>
          <Box
            sx={{
              display: 'grid',
              gap: 1.5,
              gridTemplateColumns: {
                xs: '1fr',
                md: 'minmax(240px, 2fr) repeat(3, minmax(140px, 1fr)) auto',
              },
            }}
          >
            <TextField
              label="Ara"
              onChange={(event) => setSearchInput(event.target.value)}
              placeholder="Talep no veya konu"
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
              <InputLabel id="customer-status-filter-label">Durum</InputLabel>
              <Select
                label="Durum"
                labelId="customer-status-filter-label"
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
              <InputLabel id="customer-priority-filter-label">Öncelik</InputLabel>
              <Select
                label="Öncelik"
                labelId="customer-priority-filter-label"
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
            <FormControl>
              <InputLabel id="customer-sort-label">Sırala</InputLabel>
              <Select
                label="Sırala"
                labelId="customer-sort-label"
                onChange={(event: SelectChangeEvent) =>
                  updateViewState({ page: 1, sort: event.target.value as SortOption })
                }
                value={viewState.sort}
              >
                <MenuItem value={TicketListSort.UpdatedNewest}>Son Güncellenen</MenuItem>
                <MenuItem value={TicketListSort.NewestTicket}>En Yeni</MenuItem>
                <MenuItem value={TicketListSort.OldestTicket}>En Eski</MenuItem>
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

          <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
            <FilterAltOutlined color="action" fontSize="small" />
            <Typography color="text.secondary" variant="body2">
              {pagination.totalCount} talep
            </Typography>
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
        <Box sx={{ display: 'grid', minHeight: 280, placeItems: 'center' }}>
          <CircularProgress aria-label="Destek talepleri yükleniyor" />
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
          actionLabel="Yeni Talep Oluştur"
          description="Arama ve filtrelerle eşleşen bir kayıt bulunamadı."
          icon={<Search />}
          onAction={filtersActive ? clearFilters : () => navigate('/customer/tickets/new')}
          title={tickets.length === 0 ? 'Henüz destek talebi yok' : 'Kayıt bulunamadı'}
        />
      ) : (
        <>
          <TableContainer
            component={Paper}
            sx={{
              border: '1px solid',
              borderColor: 'divider',
              display: { xs: 'none', md: 'block' },
            }}
          >
            <Table aria-label="Müşteri talepleri tablosu">
              <TableHead>
                <TableRow>
                  <TableCell>Talep</TableCell>
                  <TableCell>Konu</TableCell>
                  <TableCell>Durum</TableCell>
                  <TableCell>Öncelik</TableCell>
                  <TableCell>Kategori</TableCell>
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
                    onKeyDown={(event) => handleRowKeyDown(event, ticket.id)}
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
                      <TicketStatusChip status={ticket.status} />
                    </TableCell>
                    <TableCell>
                      <TicketPriorityChip priority={ticket.priority} />
                    </TableCell>
                    <TableCell><TicketCategoryChip category={ticket.category} /></TableCell>
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
                onKeyDown={(event) => handleRowKeyDown(event, ticket.id)}
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
                  {ticket.ticketNumber}
                </Typography>
                <Typography sx={{ fontWeight: 700, mt: 0.5 }} variant="body2">
                  {ticket.title}
                </Typography>
                <Stack direction="row" spacing={0.8} sx={{ flexWrap: 'wrap', mt: 1.2 }}>
                  <TicketStatusChip status={ticket.status} />
                  <TicketPriorityChip priority={ticket.priority} />
                  <TicketCategoryChip category={ticket.category} />
                </Stack>
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
