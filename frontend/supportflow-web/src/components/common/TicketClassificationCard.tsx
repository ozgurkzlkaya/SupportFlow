import { SaveOutlined } from '@mui/icons-material'
import {
  Alert,
  Autocomplete,
  Box,
  Button,
  CircularProgress,
  Divider,
  Paper,
  Stack,
  TextField,
  Typography,
} from '@mui/material'
import { useEffect, useRef, useState } from 'react'
import { getFriendlyApiErrorMessage } from '../../utils/getApiErrorMessage'
import { updateClassification } from '../../services/ticketService'
import { getAssignableCategories, getAssignableTags } from '../../services/taxonomyService'
import type { TicketDetail } from '../../types/ticket'
import type { TicketClassificationItem } from '../../types/taxonomy'
import { TicketCategoryChip } from './TicketCategoryChip'
import { TicketTagChip } from './TicketTagChip'

interface TicketClassificationCardProps {
  ticket: TicketDetail
  editable?: boolean
  onSaved?: (classification: { category: TicketClassificationItem | null; tags: TicketClassificationItem[] }) => void
}

function mergeOptions(
  activeOptions: TicketClassificationItem[],
  currentOptions: TicketClassificationItem[],
): TicketClassificationItem[] {
  const byId = new Map(activeOptions.map((item) => [item.id, item]))
  currentOptions.forEach((item) => byId.set(item.id, item))
  return [...byId.values()].sort((first, second) => first.name.localeCompare(second.name, 'tr'))
}

export function TicketClassificationCard({ ticket, editable = false, onSaved }: TicketClassificationCardProps) {
  const [categories, setCategories] = useState<TicketClassificationItem[]>([])
  const [tags, setTags] = useState<TicketClassificationItem[]>([])
  const [draftCategory, setDraftCategory] = useState<TicketClassificationItem | null>(ticket.category)
  const [draftTags, setDraftTags] = useState<TicketClassificationItem[]>(ticket.tags)
  const [loading, setLoading] = useState(editable)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')
  const dirtyRef = useRef(false)
  const requestRef = useRef(0)

  useEffect(() => {
    if (editable && !dirtyRef.current) {
      setDraftCategory(ticket.category)
      setDraftTags(ticket.tags)
    }
  }, [editable, ticket.category, ticket.id, ticket.tags])

  useEffect(() => {
    if (!editable) return

    const requestId = requestRef.current + 1
    requestRef.current = requestId
    let active = true
    setLoading(true)

    Promise.all([getAssignableCategories(), getAssignableTags()])
      .then(([categoryResponse, tagResponse]) => {
        if (!active || requestRef.current !== requestId) return
        setCategories(categoryResponse)
        setTags(tagResponse)
        setError('')
      })
      .catch((requestError: unknown) => {
        if (!active || requestRef.current !== requestId) return
        setError(getFriendlyApiErrorMessage(requestError, {
          fallback: 'Sınıflandırma seçenekleri yüklenemedi. Lütfen tekrar deneyin.',
          forbiddenMessage: 'Sınıflandırma seçeneklerini görüntüleme yetkiniz bulunmuyor.',
        }))
      })
      .finally(() => {
        if (active && requestRef.current === requestId) setLoading(false)
      })

    return () => { active = false }
  }, [editable])

  const categoryOptions = editable
    ? mergeOptions(categories, ticket.category ? [ticket.category] : [])
    : []
  const tagOptions = editable ? mergeOptions(tags, ticket.tags) : []

  function markDirty() {
    dirtyRef.current = true
    setSuccess('')
    setError('')
  }

  async function save() {
    setSaving(true)
    setError('')
    setSuccess('')
    try {
      const classification = await updateClassification(ticket.id, {
        categoryId: draftCategory?.id ?? null,
        tagIds: draftTags.map((tag) => tag.id),
      })
      dirtyRef.current = false
      setDraftCategory(classification.category)
      setDraftTags(classification.tags)
      onSaved?.(classification)
      setSuccess('Sınıflandırma kaydedildi.')
    } catch (requestError: unknown) {
      setError(getFriendlyApiErrorMessage(requestError, {
        fallback: 'Sınıflandırma kaydedilemedi. Lütfen tekrar deneyin.',
        forbiddenMessage: 'Bu talebi sınıflandırma yetkiniz bulunmuyor.',
      }))
    } finally {
      setSaving(false)
    }
  }

  return (
    <Paper sx={{ border: '1px solid', borderColor: 'divider', p: 3 }}>
      <Stack spacing={2}>
        <Box>
          <Typography sx={{ fontWeight: 700 }} variant="h6">Sınıflandırma</Typography>
          <Typography color="text.secondary" sx={{ mt: 0.5 }} variant="body2">
            Talebin kategorisini ve destek ekibi için etiketlerini yönetin.
          </Typography>
        </Box>

        {editable ? (
          <>
            {loading ? <CircularProgress aria-label="Sınıflandırma seçenekleri yükleniyor" size={24} /> : null}
            {error ? <Alert severity="error">{error}</Alert> : null}
            {success ? <Alert severity="success">{success}</Alert> : null}
            <Autocomplete
              clearOnEscape
              getOptionLabel={(option) => `${option.name}${option.isActive ? '' : ' (Pasif)'}`}
              isOptionEqualToValue={(option, value) => option.id === value.id}
              loading={loading}
              onChange={(_, value) => { markDirty(); setDraftCategory(value) }}
              options={categoryOptions}
              renderInput={(params) => <TextField {...params} label="Kategori" placeholder="Sınıflandırılmamış" />}
              value={draftCategory}
            />
            {categories.length === 0 && !loading ? (
              <Typography color="text.secondary" variant="caption">Tanımlı aktif kategori bulunmuyor.</Typography>
            ) : null}
            <Autocomplete
              filterSelectedOptions
              getOptionDisabled={(option) => !option.isActive && !ticket.tags.some((tag) => tag.id === option.id)}
              getOptionLabel={(option) => `${option.name}${option.isActive ? '' : ' (Pasif)'}`}
              isOptionEqualToValue={(option, value) => option.id === value.id}
              loading={loading}
              multiple
              onChange={(_, value) => { markDirty(); setDraftTags(value) }}
              options={tagOptions}
              renderInput={(params) => <TextField {...params} label="Etiketler" placeholder="Etiket yok" />}
              value={draftTags}
            />
            {tags.length === 0 && !loading ? (
              <Typography color="text.secondary" variant="caption">Tanımlı aktif etiket bulunmuyor.</Typography>
            ) : null}
            <Box sx={{ display: 'flex', justifyContent: 'flex-end' }}>
              <Button
                disabled={loading || saving}
                onClick={() => void save()}
                startIcon={saving ? <CircularProgress aria-hidden="true" color="inherit" size={18} /> : <SaveOutlined />}
                variant="contained"
              >
                {saving ? 'Kaydediliyor...' : 'Kaydet'}
              </Button>
            </Box>
          </>
        ) : (
          <>
            <Divider />
            <Stack spacing={1}>
              <Typography color="text.secondary" variant="caption">Kategori</Typography>
              <Box><TicketCategoryChip category={ticket.category} /></Box>
            </Stack>
          </>
        )}

        {editable ? (
          <>
            <Divider />
            <Stack spacing={1}>
              <Typography color="text.secondary" variant="caption">Mevcut etiketler</Typography>
              {ticket.tags.length === 0 ? (
                <Typography color="text.secondary" variant="body2">Etiket yok</Typography>
              ) : (
                <Stack direction="row" spacing={0.75} sx={{ flexWrap: 'wrap' }}>
                  {ticket.tags.map((tag) => <TicketTagChip key={tag.id} tag={tag} />)}
                </Stack>
              )}
            </Stack>
          </>
        ) : null}
      </Stack>
    </Paper>
  )
}
