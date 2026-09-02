import { AddOutlined, EditOutlined, Search, ToggleOffOutlined, ToggleOnOutlined } from '@mui/icons-material'
import {
  Alert, Box, Button, CircularProgress, Dialog, DialogActions, DialogContent, DialogTitle,
  FormControl, InputAdornment, InputLabel, LinearProgress, MenuItem, Paper, Select, Stack,
  Tab, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Tabs, TextField,
  Typography, type SelectChangeEvent,
} from '@mui/material'
import { useEffect, useRef, useState } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { EmptyStateCard } from '../../components/common/EmptyStateCard'
import { PageHeader } from '../../components/common/PageHeader'
import { PaginationControls } from '../../components/common/PaginationControls'
import {
  createCategory, createTag, getCategories, getTags, updateCategory, updateCategoryStatus,
  updateTag, updateTagStatus,
} from '../../services/taxonomyService'
import type { PaginationMetadata } from '../../types/pagination'
import type { TaxonomyStatusFilter, TicketCategory, TicketTag } from '../../types/taxonomy'
import { formatDate } from '../../utils/formatDate'
import { getFriendlyApiErrorMessage } from '../../utils/getApiErrorMessage'

type TaxonomyTab = 'categories' | 'tags'
type StatusFilter = TaxonomyStatusFilter | 'All'
type EditableTaxonomy = TicketCategory | TicketTag

interface TaxonomyViewState { tab: TaxonomyTab; search: string; status: StatusFilter; page: number; pageSize: number }

const emptyPagination: PaginationMetadata = { page: 1, pageSize: 25, totalCount: 0, totalPages: 0, hasPreviousPage: false, hasNextPage: false }

function getPositiveInteger(value: string | null, fallback: number) {
  const parsed = Number(value)
  return Number.isInteger(parsed) && parsed > 0 ? parsed : fallback
}

function getViewState(search: string): TaxonomyViewState {
  const params = new URLSearchParams(search)
  const pageSize = getPositiveInteger(params.get('pageSize'), 25)
  return {
    tab: params.get('tab') === 'tags' ? 'tags' : 'categories',
    search: params.get('search') ?? '',
    status: params.get('status') === 'Active' || params.get('status') === 'Inactive' ? params.get('status') as TaxonomyStatusFilter : 'All',
    page: getPositiveInteger(params.get('page'), 1),
    pageSize: [25, 50, 100].includes(pageSize) ? pageSize : 25,
  }
}

function createSearch(state: TaxonomyViewState) {
  const params = new URLSearchParams({ tab: state.tab, page: String(state.page), pageSize: String(state.pageSize) })
  if (state.search.trim()) params.set('search', state.search.trim())
  if (state.status !== 'All') params.set('status', state.status)
  return params.toString()
}

export function AdminTaxonomyPage() {
  const location = useLocation()
  const navigate = useNavigate()
  const viewState = getViewState(location.search)
  const [categories, setCategories] = useState<TicketCategory[]>([])
  const [tags, setTags] = useState<TicketTag[]>([])
  const [pagination, setPagination] = useState<PaginationMetadata>(emptyPagination)
  const [loading, setLoading] = useState(true)
  const [loaded, setLoaded] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [searchInput, setSearchInput] = useState(viewState.search)
  const [reloadKey, setReloadKey] = useState(0)
  const [editor, setEditor] = useState<EditableTaxonomy | 'new' | null>(null)
  const [statusTarget, setStatusTarget] = useState<EditableTaxonomy | null>(null)
  const latestRequest = useRef(0)

  function updateViewState(changes: Partial<TaxonomyViewState>, replace = false) {
    const next = { ...viewState, ...changes }
    navigate({ pathname: location.pathname, search: `?${createSearch(next)}` }, { replace })
  }

  useEffect(() => { setSearchInput(viewState.search) }, [viewState.search])

  useEffect(() => {
    if (searchInput === viewState.search) return
    const timeout = window.setTimeout(() => {
      const next = { ...getViewState(location.search), search: searchInput, page: 1 }
      navigate({ pathname: location.pathname, search: `?${createSearch(next)}` })
    }, 375)
    return () => window.clearTimeout(timeout)
  }, [location.pathname, location.search, navigate, searchInput, viewState.search])

  useEffect(() => {
    const requestId = latestRequest.current + 1
    latestRequest.current = requestId
    setLoading(true)
    setError('')
    const request = { search: viewState.search.trim() || undefined, status: viewState.status === 'All' ? undefined : viewState.status, page: viewState.page, pageSize: viewState.pageSize }
    const load = viewState.tab === 'categories' ? getCategories(request) : getTags(request)
    void load.then((response) => {
      if (latestRequest.current !== requestId) return
      if (response.pagination.totalPages > 0 && viewState.page > response.pagination.totalPages) {
        const next = { ...getViewState(location.search), page: response.pagination.totalPages }
        navigate({ pathname: location.pathname, search: `?${createSearch(next)}` }, { replace: true })
        return
      }
      if (viewState.tab === 'categories') setCategories(response.items as TicketCategory[])
      else setTags(response.items as TicketTag[])
      setPagination(response.pagination)
      setLoaded(true)
    }).catch((requestError: unknown) => {
      if (latestRequest.current === requestId) {
        setError(getFriendlyApiErrorMessage(requestError, { fallback: 'Sınıflandırma kayıtları yüklenemedi. Lütfen tekrar deneyin.' }))
      }
    }).finally(() => { if (latestRequest.current === requestId) setLoading(false) })
  }, [location.pathname, location.search, navigate, reloadKey, viewState.page, viewState.pageSize, viewState.search, viewState.status, viewState.tab])

  const items = viewState.tab === 'categories' ? categories : tags
  const isCategoryTab = viewState.tab === 'categories'
  const filtersActive = viewState.search.trim() !== '' || viewState.status !== 'All'

  async function saveEditor(values: { name: string; description: string }) {
    const name = values.name.trim()
    if (!name) throw new Error('Kategori veya etiket adı gereklidir.')
    if (isCategoryTab) {
      const request = { name, description: values.description.trim() || null }
      if (editor === 'new') await createCategory(request)
      else await updateCategory(editor!.id, request)
    } else {
      const request = { name }
      if (editor === 'new') await createTag(request)
      else await updateTag(editor!.id, request)
    }
    setEditor(null)
    setNotice(isCategoryTab ? 'Kategori kaydedildi.' : 'Etiket kaydedildi.')
    setReloadKey((key) => key + 1)
  }

  async function changeStatus() {
    if (!statusTarget) return
    try {
      if (isCategoryTab) await updateCategoryStatus(statusTarget.id, !statusTarget.isActive)
      else await updateTagStatus(statusTarget.id, !statusTarget.isActive)
      setNotice(statusTarget.isActive ? 'Durum pasif olarak güncellendi.' : 'Durum aktif olarak güncellendi.')
      setStatusTarget(null)
      setReloadKey((key) => key + 1)
    } catch (requestError: unknown) {
      setError(getFriendlyApiErrorMessage(requestError, { fallback: 'Durum güncellenemedi. Lütfen tekrar deneyin.' }))
      setStatusTarget(null)
    }
  }

  return (
    <Stack spacing={3}>
      <PageHeader description="Kategorileri ve etiketleri güvenli biçimde yönetin; mevcut taleplerin tarihsel sınıflandırmaları korunur." eyebrow="Yönetim" title="Kategoriler ve Etiketler" />
      <Paper sx={{ border: '1px solid', borderColor: 'divider' }}>
        <Tabs onChange={(_, tab: TaxonomyTab) => updateViewState({ tab, page: 1 })} value={viewState.tab} variant="fullWidth">
          <Tab label="Kategoriler" value="categories" />
          <Tab label="Etiketler" value="tags" />
        </Tabs>
      </Paper>
      <Paper sx={{ border: '1px solid', borderColor: 'divider', p: 2 }}>
        <Stack spacing={1.5}>
          <Box sx={{ display: 'grid', gap: 1.5, gridTemplateColumns: { xs: '1fr', md: 'minmax(260px, 2fr) minmax(160px, 1fr) auto' } }}>
            <TextField label="Ara" onChange={(event) => setSearchInput(event.target.value)} placeholder={isCategoryTab ? 'Kategori ara' : 'Etiket ara'} slotProps={{ input: { startAdornment: <InputAdornment position="start"><Search aria-hidden="true" /></InputAdornment> } }} value={searchInput} />
            <FormControl><InputLabel id="taxonomy-status-label">Durum</InputLabel><Select label="Durum" labelId="taxonomy-status-label" onChange={(event: SelectChangeEvent) => updateViewState({ page: 1, status: event.target.value as StatusFilter })} value={viewState.status}><MenuItem value="All">Tüm Durumlar</MenuItem><MenuItem value="Active">Aktif</MenuItem><MenuItem value="Inactive">Pasif</MenuItem></Select></FormControl>
            <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
              {filtersActive ? <Button onClick={() => { setSearchInput(''); updateViewState({ search: '', status: 'All', page: 1 }) }} variant="outlined">Temizle</Button> : null}
              <Button onClick={() => { setNotice(''); setEditor('new') }} startIcon={<AddOutlined />} variant="contained">{isCategoryTab ? 'Yeni Kategori' : 'Yeni Etiket'}</Button>
            </Stack>
          </Box>
          <Typography color="text.secondary" variant="body2">{pagination.totalCount} kayıt</Typography>
        </Stack>
      </Paper>
      {notice ? <Alert onClose={() => setNotice('')} severity="success">{notice}</Alert> : null}
      {loading && loaded ? <LinearProgress aria-label="Sınıflandırma kayıtları güncelleniyor" /> : null}
      {loading && !loaded ? <Box sx={{ display: 'grid', minHeight: 260, placeItems: 'center' }}><CircularProgress aria-label="Sınıflandırma kayıtları yükleniyor" /></Box> : error ? <Alert action={<Button color="inherit" onClick={() => setReloadKey((key) => key + 1)}>Tekrar Dene</Button>} severity="error">{error}</Alert> : items.length === 0 ? <EmptyStateCard description={filtersActive ? 'Arama ve filtrelerle eşleşen kayıt bulunamadı.' : isCategoryTab ? 'Henüz kategori bulunmuyor.' : 'Henüz etiket bulunmuyor.'} icon={<Search />} title="Kayıt bulunamadı" /> : <>
        <TableContainer component={Paper} sx={{ border: '1px solid', borderColor: 'divider', overflowX: 'auto' }}><Table aria-label={isCategoryTab ? 'Kategoriler' : 'Etiketler'}><TableHead><TableRow><TableCell>Ad</TableCell>{isCategoryTab ? <TableCell>Açıklama</TableCell> : null}<TableCell>Durum</TableCell><TableCell align="right">Kullanım</TableCell><TableCell>Oluşturulma</TableCell><TableCell align="right">İşlemler</TableCell></TableRow></TableHead><TableBody>{items.map((item) => <TableRow key={item.id}><TableCell sx={{ fontWeight: 700 }}>{item.name}</TableCell>{isCategoryTab ? <TableCell>{(item as TicketCategory).description || '-'}</TableCell> : null}<TableCell>{item.isActive ? 'Aktif' : 'Pasif'}</TableCell><TableCell align="right">{item.usageCount}</TableCell><TableCell>{formatDate(item.createdAt)}</TableCell><TableCell align="right"><Stack direction="row" spacing={0.5} sx={{ justifyContent: 'flex-end' }}><Button aria-label={`${item.name} düzenle`} onClick={() => { setNotice(''); setEditor(item) }} size="small" startIcon={<EditOutlined />}>Düzenle</Button><Button color={item.isActive ? 'warning' : 'success'} onClick={() => setStatusTarget(item)} size="small" startIcon={item.isActive ? <ToggleOffOutlined /> : <ToggleOnOutlined />}>{item.isActive ? 'Pasifleştir' : 'Aktifleştir'}</Button></Stack></TableCell></TableRow>)}</TableBody></Table></TableContainer>
        <PaginationControls itemLabel="kayıt" onPageChange={(page) => updateViewState({ page })} onPageSizeChange={(pageSize) => updateViewState({ page: 1, pageSize })} pagination={pagination} />
      </>}
      <TaxonomyEditorDialog category={isCategoryTab} item={editor} onClose={() => setEditor(null)} onSave={saveEditor} />
      <Dialog onClose={() => setStatusTarget(null)} open={Boolean(statusTarget)}><DialogTitle>{statusTarget?.isActive ? 'Pasifleştirmeyi onaylayın' : 'Aktifleştirmeyi onaylayın'}</DialogTitle><DialogContent><Stack spacing={1.5} sx={{ pt: 1 }}><Typography>{statusTarget?.isActive ? isCategoryTab ? 'Bu kategori yeni taleplere atanamaz ancak mevcut taleplerde görünmeye devam eder.' : 'Bu etiket yeni taleplere eklenemez ancak mevcut taleplerde korunur.' : 'Bu kayıt yeniden yeni taleplerde kullanılabilir.'}</Typography>{statusTarget && statusTarget.usageCount > 0 ? <Alert severity="info">Bu kayıt {statusTarget.usageCount} mevcut talepte kullanılıyor.</Alert> : null}</Stack></DialogContent><DialogActions><Button onClick={() => setStatusTarget(null)}>Vazgeç</Button><Button color={statusTarget?.isActive ? 'warning' : 'primary'} onClick={() => void changeStatus()} variant="contained">Onayla</Button></DialogActions></Dialog>
    </Stack>
  )
}

function TaxonomyEditorDialog({ category, item, onClose, onSave }: { category: boolean; item: EditableTaxonomy | 'new' | null; onClose: () => void; onSave: (values: { name: string; description: string }) => Promise<void> }) {
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [error, setError] = useState('')
  const [saving, setSaving] = useState(false)
  useEffect(() => { setName(item && item !== 'new' ? item.name : ''); setDescription(item && item !== 'new' && 'description' in item ? item.description || '' : ''); setError('') }, [item])
  async function submit() { if (!name.trim()) { setError('Ad alanı zorunludur.'); return }; setSaving(true); setError(''); try { await onSave({ name, description }) } catch (requestError: unknown) { setError(getFriendlyApiErrorMessage(requestError, { fallback: 'Kayıt kaydedilemedi. Aynı adla başka bir kayıt olmadığını kontrol edin.', conflictMessage: 'Bu adla zaten bir kayıt bulunuyor.' })) } finally { setSaving(false) } }
  const label = category ? 'Kategori' : 'Etiket'
  return <Dialog fullWidth maxWidth="sm" onClose={onClose} open={item !== null}><DialogTitle>{item === 'new' ? `Yeni ${label}` : `${label} Düzenle`}</DialogTitle><DialogContent><Stack spacing={2} sx={{ pt: 1 }}>{error ? <Alert severity="error">{error}</Alert> : null}<TextField autoFocus disabled={saving} label="Ad" onChange={(event) => setName(event.target.value)} required slotProps={{ htmlInput: { maxLength: category ? 150 : 100 } }} value={name} />{category ? <TextField disabled={saving} label="Açıklama" minRows={3} multiline onChange={(event) => setDescription(event.target.value)} slotProps={{ htmlInput: { maxLength: 500 } }} value={description} /> : null}</Stack></DialogContent><DialogActions><Button disabled={saving} onClick={onClose}>Vazgeç</Button><Button disabled={saving} onClick={() => void submit()} variant="contained">{saving ? 'Kaydediliyor...' : 'Kaydet'}</Button></DialogActions></Dialog>
}
