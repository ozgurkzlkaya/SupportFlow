import { ArrowBack, Send, UploadFileOutlined } from '@mui/icons-material'
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Divider,
  Paper,
  Snackbar,
  Stack,
  TextField,
  Typography,
} from '@mui/material'
import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { useLocation, useNavigate, useParams } from 'react-router-dom'
import { StatusHistoryTimeline } from '../../components/common/StatusHistoryTimeline'
import { TicketActivityTimeline } from '../../components/common/TicketActivityTimeline'
import { TicketAttachmentList } from '../../components/common/TicketAttachmentList'
import { TicketAttachmentPicker } from '../../components/common/TicketAttachmentPicker'
import { TicketConversationTimeline } from '../../components/common/TicketConversationTimeline'
import { TicketPriorityChip } from '../../components/common/TicketPriorityChip'
import { TicketCategoryChip } from '../../components/common/TicketCategoryChip'
import { TicketStatusChip } from '../../components/common/TicketStatusChip'
import {
  getTicketAttachments,
  uploadTicketAttachment,
} from '../../services/attachmentService'
import { getHistory, getMessages, getTicket, sendMessage } from '../../services/ticketService'
import type { TicketAttachment } from '../../types/attachment'
import type { TicketDetail, TicketMessage, TicketStatusHistory } from '../../types/ticket'
import { UserRole } from '../../types/auth'
import { formatDate } from '../../utils/formatDate'
import { getFriendlyApiErrorMessage } from '../../utils/getApiErrorMessage'

interface TicketPageLocationState {
  ticketCreated?: boolean
  uploadedAttachmentCount?: number
  failedAttachmentNames?: string[]
}

const maxAttachmentCount = 5
const maxAttachmentSizeBytes = 5 * 1024 * 1024
const allowedAttachmentTypes = new Set([
  'image/png',
  'image/jpeg',
  'image/webp',
  'application/pdf',
])

function createUploadedAttachmentNameSet(attachments: TicketAttachment[]) {
  return new Set(attachments.map((attachment) => attachment.fileName))
}

function getAttachmentLoadErrorMessage(error: unknown) {
  return getFriendlyApiErrorMessage(error, {
    fallback: 'Ek dosyalar yüklenemedi. Lütfen tekrar deneyin.',
    forbiddenMessage: 'Bu işlemi görüntüleme yetkiniz bulunmuyor.',
    notFoundMessage: 'Talep bulunamadı.',
    unavailableMessage:
      'SupportFlow hizmetine ulaşılamıyor. Lütfen daha sonra tekrar deneyin.',
  })
}

function getAttachmentUploadErrorMessage(error: unknown) {
  return getFriendlyApiErrorMessage(error, {
    fallback: 'Dosya yüklenemedi. Lütfen tekrar deneyin.',
    forbiddenMessage: 'Bu işlem için yetkiniz bulunmuyor.',
    notFoundMessage: 'Talep bulunamadı.',
    unavailableMessage:
      'SupportFlow hizmetine ulaşılamıyor. Lütfen daha sonra tekrar deneyin.',
  })
}

export function CustomerTicketDetailPage() {
  const navigate = useNavigate()
  const location = useLocation()
  const { id } = useParams()
  const ticketId = Number(id)
  const validTicketId = Number.isInteger(ticketId) && ticketId > 0
  const locationState = (location.state as TicketPageLocationState | null) ?? null
  const ticketCreated = Boolean(locationState?.ticketCreated)
  const uploadedAttachmentCount = locationState?.uploadedAttachmentCount ?? 0
  const initialFailedAttachmentNames = locationState?.failedAttachmentNames ?? []

  const [ticket, setTicket] = useState<TicketDetail | null>(null)
  const [messages, setMessages] = useState<TicketMessage[]>([])
  const [history, setHistory] = useState<TicketStatusHistory[]>([])
  const [attachments, setAttachments] = useState<TicketAttachment[]>([])
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState('')
  const [attachmentLoadError, setAttachmentLoadError] = useState('')
  const [reloadKey, setReloadKey] = useState(0)
  const [timelineRefreshKey, setTimelineRefreshKey] = useState(0)
  const [reply, setReply] = useState('')
  const [replyError, setReplyError] = useState('')
  const [submittingReply, setSubmittingReply] = useState(false)
  const [replySent, setReplySent] = useState(false)
  const [pendingAttachmentFiles, setPendingAttachmentFiles] = useState<File[]>([])
  const [pendingAttachmentError, setPendingAttachmentError] = useState('')
  const [attachmentUploadSuccessMessage, setAttachmentUploadSuccessMessage] = useState('')
  const [failedAttachmentNames, setFailedAttachmentNames] = useState<string[]>(
    initialFailedAttachmentNames,
  )
  const [uploadingAttachments, setUploadingAttachments] = useState(false)

  useEffect(() => {
    if (!locationState) {
      return
    }

    navigate(location.pathname, { replace: true, state: null })
  }, [location.pathname, locationState, navigate])

  useEffect(() => {
    let active = true

    async function loadTicketData() {
      if (!validTicketId) {
        setLoadError('Geçersiz destek talebi numarası.')
        setLoading(false)
        return
      }

      setLoading(true)
      setLoadError('')
      setAttachmentLoadError('')

      try {
        const [ticketResponse, messageResponse, historyResponse] = await Promise.all([
          getTicket(ticketId),
          getMessages(ticketId),
          getHistory(ticketId),
        ])

        if (!active) {
          return
        }

        setTicket(ticketResponse)
        setMessages(messageResponse.filter((message) => !message.isInternal))
        setHistory(historyResponse)

        try {
          const attachmentResponse = await getTicketAttachments(ticketId)

          if (active) {
            setAttachments(attachmentResponse)
            const uploadedAttachmentNames = createUploadedAttachmentNameSet(attachmentResponse)
            setFailedAttachmentNames((currentNames) =>
              currentNames.filter((name) => !uploadedAttachmentNames.has(name)),
            )
          }
        } catch (error: unknown) {
          if (active) {
            setAttachments([])
            setAttachmentLoadError(getAttachmentLoadErrorMessage(error))
          }
        }
      } catch (error: unknown) {
        if (active) {
          setLoadError(
            getFriendlyApiErrorMessage(error, {
              fallback: 'Destek talebi yüklenemedi. Lütfen tekrar deneyin.',
              forbiddenMessage: 'Bu işlem için yetkiniz bulunmuyor.',
              notFoundMessage: 'Talep bulunamadı.',
              unavailableMessage:
                'SupportFlow hizmetine ulaşılamıyor. Lütfen daha sonra tekrar deneyin.',
            }),
          )
        }
      } finally {
        if (active) {
          setLoading(false)
        }
      }
    }

    void loadTicketData()

    return () => {
      active = false
    }
  }, [reloadKey, ticketId, validTicketId])

  const latestMessageAt = useMemo(() => {
    if (messages.length === 0) {
      return ticket?.updatedAt ?? null
    }

    return [...messages]
      .sort(
        (first, second) =>
          new Date(second.createdAt).getTime() - new Date(first.createdAt).getTime(),
      )[0]?.createdAt
  }, [messages, ticket?.updatedAt])

  const attachmentSlotsRemaining = Math.max(0, maxAttachmentCount - attachments.length)

  function validateAttachmentBatch(files: File[]) {
    if (files.length === 0) {
      return ''
    }

    if (files.length > attachmentSlotsRemaining) {
      return attachmentSlotsRemaining === 0
        ? 'Bu talep için en fazla 5 ek dosya saklanabilir.'
        : `Bu talebe en fazla ${attachmentSlotsRemaining} dosya daha ekleyebilirsiniz.`
    }

    const invalidTypeFile = files.find((file) => !allowedAttachmentTypes.has(file.type))
    if (invalidTypeFile) {
      return `İzin verilmeyen dosya türü: ${invalidTypeFile.name}`
    }

    const oversizedFile = files.find((file) => file.size > maxAttachmentSizeBytes)
    if (oversizedFile) {
      return `5 MB sınırını aşan dosya: ${oversizedFile.name}`
    }

    return ''
  }

  async function reloadAttachments(currentTicketId: number) {
    try {
      const attachmentResponse = await getTicketAttachments(currentTicketId)
      setAttachments(attachmentResponse)
      setAttachmentLoadError('')
      return attachmentResponse
    } catch (error: unknown) {
      setAttachments([])
      setAttachmentLoadError(getAttachmentLoadErrorMessage(error))
      return null
    }
  }

  function handleAddPendingFiles(files: File[]) {
    const mergedFiles = [...pendingAttachmentFiles, ...files].slice(0, attachmentSlotsRemaining)
    const validationError =
      files.length + pendingAttachmentFiles.length > attachmentSlotsRemaining
        ? attachmentSlotsRemaining === 0
          ? 'Bu talep için en fazla 5 ek dosya saklanabilir.'
          : `Bu talebe en fazla ${attachmentSlotsRemaining} dosya daha ekleyebilirsiniz.`
        : validateAttachmentBatch(mergedFiles)

    setPendingAttachmentError(validationError)
    setAttachmentUploadSuccessMessage('')

    if (!validationError) {
      setPendingAttachmentFiles(mergedFiles)
    }
  }

  function handleRemovePendingFile(index: number) {
    setPendingAttachmentFiles((currentFiles) =>
      currentFiles.filter((_, fileIndex) => fileIndex !== index),
    )
    setPendingAttachmentError('')
    setAttachmentUploadSuccessMessage('')
  }

  async function handleAttachmentUpload() {
    if (!ticket) {
      return
    }

    const validationError = validateAttachmentBatch(pendingAttachmentFiles)
    if (validationError) {
      setPendingAttachmentError(validationError)
      return
    }

    if (pendingAttachmentFiles.length === 0) {
      setPendingAttachmentError('Yüklenecek en az bir dosya seçin.')
      return
    }

    setUploadingAttachments(true)
    setPendingAttachmentError('')
    setAttachmentUploadSuccessMessage('')

    const failedFiles: File[] = []
    let failedReason = ''
    let successfulUploadCount = 0

    try {
      for (const file of pendingAttachmentFiles) {
        try {
          await uploadTicketAttachment(ticket.id, file)
          successfulUploadCount += 1
        } catch (error: unknown) {
          failedFiles.push(file)
          failedReason = failedReason || getAttachmentUploadErrorMessage(error)
        }
      }

      const attachmentResponse = await reloadAttachments(ticket.id)
      const uploadedAttachmentNames = createUploadedAttachmentNameSet(attachmentResponse ?? [])
      const remainingFailedFiles = failedFiles.filter(
        (file) => !uploadedAttachmentNames.has(file.name),
      )
      const remainingFailedNames = remainingFailedFiles.map((file) => file.name)

      setPendingAttachmentFiles(remainingFailedFiles)
      setFailedAttachmentNames(remainingFailedNames)

      if (successfulUploadCount > 0) {
        setTimelineRefreshKey((key) => key + 1)
        setAttachmentUploadSuccessMessage(
          successfulUploadCount === 1
            ? 'Ek dosya başarıyla yüklendi.'
            : `${successfulUploadCount} ek dosya başarıyla yüklendi.`,
        )
      }

      if (remainingFailedNames.length > 0) {
        setPendingAttachmentError(
          failedReason || `Bazı dosyalar yüklenemedi: ${remainingFailedNames.join(', ')}`,
        )
      } else {
        setPendingAttachmentError('')
      }
    } catch (error: unknown) {
      setPendingAttachmentError(getAttachmentUploadErrorMessage(error))
    } finally {
      setUploadingAttachments(false)
    }
  }

  async function handleReply(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const trimmedReply = reply.trim()

    if (!trimmedReply) {
      setReplyError('Yanıt alanı boş bırakılamaz.')
      return
    }

    if (!ticket) {
      return
    }

    setSubmittingReply(true)
    setReplyError('')
    setReplySent(false)

    try {
      await sendMessage(ticket.id, {
        message: trimmedReply,
        isInternal: false,
      })

      const [updatedTicket, updatedMessages, updatedHistory] = await Promise.all([
        getTicket(ticket.id),
        getMessages(ticket.id),
        getHistory(ticket.id),
      ])
      setTicket(updatedTicket)
      setMessages(updatedMessages.filter((message) => !message.isInternal))
      setHistory(updatedHistory)
      setTimelineRefreshKey((key) => key + 1)
      setReply('')
      setReplySent(true)
    } catch (error: unknown) {
      setReplyError(
        getFriendlyApiErrorMessage(error, {
          fallback: 'Yanıtınız gönderilemedi. Lütfen tekrar deneyin.',
          forbiddenMessage: 'Bu işlem için yetkiniz bulunmuyor.',
          notFoundMessage: 'Talep bulunamadı.',
          unavailableMessage:
            'SupportFlow hizmetine ulaşılamıyor. Lütfen daha sonra tekrar deneyin.',
        }),
      )
    } finally {
      setSubmittingReply(false)
    }
  }

  if (loading) {
    return (
      <Box sx={{ display: 'grid', minHeight: 420, placeItems: 'center' }}>
        <CircularProgress aria-label="Destek talebi yükleniyor" />
      </Box>
    )
  }

  if (loadError || !ticket) {
    return (
      <Stack spacing={2}>
        <Button
          onClick={() => navigate('/customer/tickets')}
          startIcon={<ArrowBack />}
          sx={{ alignSelf: 'flex-start' }}
          variant="outlined"
        >
          Taleplerime Dön
        </Button>
        <Alert
          action={
            validTicketId ? (
              <Button color="inherit" onClick={() => setReloadKey((key) => key + 1)}>
                Tekrar Dene
              </Button>
            ) : undefined
          }
          severity="error"
        >
          {loadError || 'Talep bulunamadı.'}
        </Alert>
      </Stack>
    )
  }

  return (
    <Stack spacing={3}>
      <Button
        onClick={() => navigate('/customer/tickets')}
        startIcon={<ArrowBack />}
        sx={{ alignSelf: 'flex-start' }}
        variant="outlined"
      >
        Taleplerime Dön
      </Button>

      {ticketCreated ? (
        <Alert severity="success">Destek talebiniz başarıyla oluşturuldu.</Alert>
      ) : null}
      {ticketCreated && uploadedAttachmentCount > 0 ? (
        <Alert severity="info">{uploadedAttachmentCount} ek dosya başarıyla yüklendi.</Alert>
      ) : null}
      {failedAttachmentNames.length > 0 ? (
        <Alert severity="warning">
          <strong>Talep oluşturuldu ancak bazı ek dosyalar yüklenemedi.</strong>
          <br />
          Başarısız dosyalar: {failedAttachmentNames.join(', ')}
        </Alert>
      ) : null}

      <Paper sx={{ border: '1px solid', borderColor: 'divider', p: { xs: 2.5, sm: 3 } }}>
        <Stack spacing={1.5}>
          <Typography color="text.secondary" variant="caption">
            {ticket.ticketNumber}
          </Typography>
          <Typography sx={{ fontWeight: 700 }} variant="h4">
            {ticket.title}
          </Typography>
          <Stack direction="row" spacing={0.8} sx={{ flexWrap: 'wrap' }}>
            <TicketStatusChip size="medium" status={ticket.status} />
            <TicketPriorityChip priority={ticket.priority} size="medium" />
          </Stack>
          <Divider />
          <Stack
            direction={{ xs: 'column', md: 'row' }}
            spacing={2}
            sx={{ color: 'text.secondary', flexWrap: 'wrap' }}
          >
            <Typography variant="body2">Müşteri: {ticket.customerName}</Typography>
            <Typography variant="body2">
              Atanan: {ticket.assignedUserName || 'Henüz atanmadı'}
            </Typography>
            <Typography variant="body2">
              Son güncelleme: {formatDate(ticket.updatedAt)}
            </Typography>
          </Stack>
        </Stack>
      </Paper>

      <Box
        sx={{
          display: 'grid',
          gap: 3,
          gridTemplateColumns: { xs: '1fr', xl: 'minmax(0, 8fr) minmax(320px, 4fr)' },
        }}
      >
        <Stack spacing={3}>
          <Paper sx={{ border: '1px solid', borderColor: 'divider', p: { xs: 2.5, sm: 3 } }}>
            <Typography sx={{ fontWeight: 700, mb: 1 }} variant="h6">
              Talep Açıklaması
            </Typography>
            <Typography color="text.secondary" sx={{ whiteSpace: 'pre-wrap' }} variant="body2">
              {ticket.description}
            </Typography>
          </Paper>

          <Paper sx={{ border: '1px solid', borderColor: 'divider', p: { xs: 2.5, sm: 3 } }}>
            <Stack spacing={2}>
              <Box>
                <Typography sx={{ fontWeight: 700, mb: 1 }} variant="h6">
                  Ek Dosyalar
                </Typography>
                <Typography color="text.secondary" variant="body2">
                  Talep oluşturulurken yüklenen kanıt dosyaları burada listelenir.
                </Typography>
              </Box>

              {attachmentLoadError ? (
                <Alert severity="error">{attachmentLoadError}</Alert>
              ) : (
                <TicketAttachmentList attachments={attachments} ticketId={ticket.id} />
              )}

              <Divider />

              <Stack spacing={1.5}>
                <Box>
                  <Typography sx={{ fontWeight: 700 }} variant="body1">
                    Ek dosya yükleyin veya başarısız yüklemeyi yeniden deneyin
                  </Typography>
                  <Typography color="text.secondary" sx={{ mt: 0.5 }} variant="body2">
                    Bu aşamada talep başına en fazla {maxAttachmentCount} dosya saklanabilir.
                    Kalan hak: {attachmentSlotsRemaining}
                  </Typography>
                </Box>

                {pendingAttachmentError ? (
                  <Alert severity="error">{pendingAttachmentError}</Alert>
                ) : null}

                <TicketAttachmentPicker
                  error={pendingAttachmentError}
                  files={pendingAttachmentFiles}
                  onAddFiles={handleAddPendingFiles}
                  onRemoveFile={handleRemovePendingFile}
                />

                <Box sx={{ display: 'flex', justifyContent: 'flex-end' }}>
                  <Button
                    disabled={uploadingAttachments || attachmentSlotsRemaining === 0}
                    onClick={() => void handleAttachmentUpload()}
                    startIcon={
                      uploadingAttachments ? (
                        <CircularProgress aria-hidden="true" color="inherit" size={18} />
                      ) : (
                        <UploadFileOutlined />
                      )
                    }
                    variant="contained"
                  >
                    {uploadingAttachments ? 'Yükleniyor...' : 'Ek Dosyaları Yükle'}
                  </Button>
                </Box>
              </Stack>
            </Stack>
          </Paper>

          <Paper sx={{ border: '1px solid', borderColor: 'divider', p: { xs: 2.5, sm: 3 } }}>
            <Typography sx={{ fontWeight: 700, mb: 1 }} variant="h6">
              Konuşma
            </Typography>
            <Typography color="text.secondary" sx={{ mb: 2 }} variant="body2">
              Talep kapsamındaki tüm dış yazışmalar burada listelenir.
            </Typography>
            <TicketConversationTimeline
              emptyText="Henüz ek bir yanıt bulunmuyor."
              initialMessage={{
                senderName: ticket.customerName,
                message: ticket.description,
                createdAt: ticket.createdAt,
                markerLabel: 'İlk Talep',
              }}
              messages={messages}
            />
          </Paper>

          <Paper sx={{ border: '1px solid', borderColor: 'divider', p: { xs: 2.5, sm: 3 } }}>
            <Typography sx={{ fontWeight: 700, mb: 1.5 }} variant="h6">
              Yanıt Yaz
            </Typography>
            {replySent ? (
              <Alert severity="success" sx={{ mb: 2 }}>
                Yanıtınız gönderildi.
              </Alert>
            ) : null}
            {replyError ? (
              <Alert severity="error" sx={{ mb: 2 }}>
                {replyError}
              </Alert>
            ) : null}
            <Box component="form" noValidate onSubmit={handleReply}>
              <Stack spacing={2}>
                <TextField
                  disabled={submittingReply}
                  error={replyError === 'Yanıt alanı boş bırakılamaz.'}
                  label="Yanıtınız"
                  minRows={5}
                  multiline
                  name="reply"
                  onChange={(event) => {
                    setReply(event.target.value)
                    setReplyError('')
                    setReplySent(false)
                  }}
                  required
                  value={reply}
                />
                <Box sx={{ display: 'flex', justifyContent: 'flex-end' }}>
                  <Button
                    disabled={submittingReply}
                    startIcon={
                      submittingReply ? (
                        <CircularProgress aria-hidden="true" color="inherit" size={18} />
                      ) : (
                        <Send />
                      )
                    }
                    type="submit"
                    variant="contained"
                  >
                    {submittingReply ? 'Gönderiliyor...' : 'Yanıtı Gönder'}
                  </Button>
                </Box>
              </Stack>
            </Box>
          </Paper>

          <TicketActivityTimeline
            key={`${ticket.id}-${timelineRefreshKey}`}
            role={UserRole.Customer}
            ticketId={ticket.id}
          />
        </Stack>

        <Stack spacing={3}>
          <Paper sx={{ border: '1px solid', borderColor: 'divider', p: 3 }}>
            <Typography sx={{ fontWeight: 700, mb: 2 }} variant="h6">
              Talep Bilgileri
            </Typography>
            <Stack spacing={1.5}>
              <Typography variant="body2">
                <strong>Oluşturulma:</strong> {formatDate(ticket.createdAt)}
              </Typography>
              <Typography variant="body2">
                <strong>Son Güncelleme:</strong> {formatDate(ticket.updatedAt)}
              </Typography>
              {latestMessageAt ? (
                <Typography variant="body2">
                  <strong>Son Mesaj:</strong> {formatDate(latestMessageAt)}
                </Typography>
              ) : null}
              <Typography variant="body2">
                <strong>Atanan Teknik Personel:</strong>{' '}
                {ticket.assignedUserName || 'Henüz teknik personele atanmadı.'}
              </Typography>
              {ticket.closedAt ? (
                <Typography variant="body2">
                  <strong>Kapanış Tarihi:</strong> {formatDate(ticket.closedAt)}
                </Typography>
              ) : null}
            </Stack>
          </Paper>

          <Paper sx={{ border: '1px solid', borderColor: 'divider', p: 3 }}>
            <Stack spacing={1}>
              <Typography sx={{ fontWeight: 700 }} variant="h6">Sınıflandırma</Typography>
              <Typography color="text.secondary" variant="caption">Kategori</Typography>
              <Box><TicketCategoryChip category={ticket.category} /></Box>
            </Stack>
          </Paper>

          <Paper sx={{ border: '1px solid', borderColor: 'divider', p: 3 }}>
            <Typography sx={{ fontWeight: 700, mb: 2 }} variant="h6">
              Durum Geçmişi
            </Typography>
            <StatusHistoryTimeline
              emptyText="Bu talep için henüz durum değişikliği bulunmuyor."
              history={history}
            />
          </Paper>
        </Stack>
      </Box>

      <Snackbar
        autoHideDuration={4000}
        onClose={(_, reason) => {
          if (reason !== 'clickaway') {
            setAttachmentUploadSuccessMessage('')
          }
        }}
        open={Boolean(attachmentUploadSuccessMessage)}
      >
        <Alert
          onClose={() => setAttachmentUploadSuccessMessage('')}
          severity="success"
          sx={{ width: '100%' }}
        >
          {attachmentUploadSuccessMessage}
        </Alert>
      </Snackbar>
    </Stack>
  )
}
