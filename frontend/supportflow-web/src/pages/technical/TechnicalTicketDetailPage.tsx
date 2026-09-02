import { ArrowBack, LockOutlined, Send } from '@mui/icons-material'
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  FormControl,
  FormHelperText,
  InputLabel,
  MenuItem,
  Paper,
  Select,
  Stack,
  Tab,
  Tabs,
  TextField,
  Typography,
  type SelectChangeEvent,
} from '@mui/material'
import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { useLocation, useNavigate, useParams } from 'react-router-dom'
import { StatusHistoryTimeline } from '../../components/common/StatusHistoryTimeline'
import { TicketActivityTimeline } from '../../components/common/TicketActivityTimeline'
import { TicketAttachmentList } from '../../components/common/TicketAttachmentList'
import { TicketConversationTimeline } from '../../components/common/TicketConversationTimeline'
import { TicketPriorityChip } from '../../components/common/TicketPriorityChip'
import { TicketClassificationCard } from '../../components/common/TicketClassificationCard'
import { TicketSlaPanel } from '../../components/common/TicketSlaPanel'
import { RequesterTypeChip } from '../../components/common/RequesterTypeChip'
import { TicketStatusChip } from '../../components/common/TicketStatusChip'
import { getTicketAttachments } from '../../services/attachmentService'
import {
  assignTicket,
  getHistory,
  getMessages,
  getTicket,
  retryMessageDelivery,
  sendMessage,
  updatePriority,
  updateStatus,
} from '../../services/ticketService'
import { getTechnicalStaff } from '../../services/userService'
import type { TicketAttachment } from '../../types/attachment'
import {
  TicketPriority,
  type TicketDetail,
  type TicketMessage,
  type TicketStatus as TicketStatusType,
  type TicketStatusHistory,
} from '../../types/ticket'
import type { TechnicalStaffUser } from '../../types/user'
import { UserRole } from '../../types/auth'
import { formatDate } from '../../utils/formatDate'
import { getApiErrorStatus, getFriendlyApiErrorMessage } from '../../utils/getApiErrorMessage'
import { getTicketPriorityLabel, getTicketStatusLabel } from '../../utils/ticketLabels'
import { getAllowedNextStatuses } from '../../utils/ticketStatusTransitions'

type MessageMode = 'public' | 'internal'
type StatusSelection = TicketStatusType | ''
type PrioritySelection = TicketPriority | ''

function getDefaultNextStatus(status: TicketStatusType): StatusSelection {
  return getAllowedNextStatuses(status)[0] ?? ''
}

export function TechnicalTicketDetailPage() {
  const location = useLocation()
  const navigate = useNavigate()
  const { id } = useParams()
  const ticketId = Number(id)
  const validTicketId = Number.isInteger(ticketId) && ticketId > 0
  const routeBase = location.pathname.startsWith('/admin') ? '/admin' : '/technical'
  const ticketsPath = `${routeBase}/tickets`

  const [ticket, setTicket] = useState<TicketDetail | null>(null)
  const [messages, setMessages] = useState<TicketMessage[]>([])
  const [history, setHistory] = useState<TicketStatusHistory[]>([])
  const [attachments, setAttachments] = useState<TicketAttachment[]>([])
  const [technicalStaff, setTechnicalStaff] = useState<TechnicalStaffUser[]>([])
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState('')
  const [attachmentLoadError, setAttachmentLoadError] = useState('')
  const [reloadKey, setReloadKey] = useState(0)
  const [timelineRefreshKey, setTimelineRefreshKey] = useState(0)

  const [messageMode, setMessageMode] = useState<MessageMode>('public')
  const [messageText, setMessageText] = useState('')
  const [messageError, setMessageError] = useState('')
  const [messageSuccess, setMessageSuccess] = useState('')
  const [sendingMessage, setSendingMessage] = useState(false)
  const [retryingMessageId, setRetryingMessageId] = useState<number | null>(null)
  const [deliveryRetryError, setDeliveryRetryError] = useState('')

  const [assignedUserId, setAssignedUserId] = useState('')
  const [assignmentError, setAssignmentError] = useState('')
  const [assignmentSuccess, setAssignmentSuccess] = useState('')
  const [assigning, setAssigning] = useState(false)
  const [confirmAssignmentOpen, setConfirmAssignmentOpen] = useState(false)

  const [selectedPriority, setSelectedPriority] = useState<PrioritySelection>('')
  const [priorityError, setPriorityError] = useState('')
  const [prioritySuccess, setPrioritySuccess] = useState('')
  const [updatingPriority, setUpdatingPriority] = useState(false)

  const [selectedStatus, setSelectedStatus] = useState<StatusSelection>('')
  const [statusNote, setStatusNote] = useState('')
  const [statusError, setStatusError] = useState('')
  const [statusSuccess, setStatusSuccess] = useState('')
  const [updatingStatus, setUpdatingStatus] = useState(false)
  const [confirmStatusChangeOpen, setConfirmStatusChangeOpen] = useState(false)

  const allowedNextStatuses = useMemo(
    () => (ticket ? getAllowedNextStatuses(ticket.status) : []),
    [ticket],
  )

  const selectedAssigneeName = useMemo(() => {
    const assignee = technicalStaff.find((staff) => staff.id.toString() === assignedUserId)
    return assignee ? `${assignee.firstName} ${assignee.lastName}` : ''
  }, [assignedUserId, technicalStaff])

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
        const [ticketResponse, messageResponse, historyResponse, staffResponse] =
          await Promise.all([
            getTicket(ticketId),
            getMessages(ticketId),
            getHistory(ticketId),
            getTechnicalStaff(),
          ])

        if (!active) {
          return
        }

        setTicket(ticketResponse)
        setMessages(messageResponse)
        setHistory(historyResponse)
        setTechnicalStaff(staffResponse)
        setAssignedUserId(ticketResponse.assignedUserId?.toString() ?? '')
        setSelectedPriority(ticketResponse.priority)
        setSelectedStatus(getDefaultNextStatus(ticketResponse.status))

        try {
          const attachmentResponse = await getTicketAttachments(ticketId)
          if (active) {
            setAttachments(attachmentResponse)
          }
        } catch {
          if (active) {
            setAttachments([])
            setAttachmentLoadError('Ek dosyalar şu anda yüklenemiyor.')
          }
        }
      } catch (error: unknown) {
        if (active) {
          setLoadError(
            getFriendlyApiErrorMessage(error, {
              fallback: 'Talep ayrıntıları yüklenemedi. Lütfen tekrar deneyin.',
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

  async function refreshTicketWorkspace(currentTicketId: number) {
    const [ticketResponse, messageResponse, historyResponse] = await Promise.all([
      getTicket(currentTicketId),
      getMessages(currentTicketId),
      getHistory(currentTicketId),
    ])

    setTicket(ticketResponse)
    setMessages(messageResponse)
    setHistory(historyResponse)
    setAssignedUserId(ticketResponse.assignedUserId?.toString() ?? '')
    setSelectedPriority(ticketResponse.priority)
    setSelectedStatus(getDefaultNextStatus(ticketResponse.status))

    try {
      const attachmentResponse = await getTicketAttachments(currentTicketId)
      setAttachments(attachmentResponse)
      setAttachmentLoadError('')
    } catch {
      setAttachments([])
      setAttachmentLoadError('Ek dosyalar şu anda yüklenemiyor.')
    }
  }

  async function handleMessageSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const trimmedMessage = messageText.trim()

    if (!trimmedMessage) {
      setMessageError(
        messageMode === 'internal'
          ? 'İç not alanı boş bırakılamaz.'
          : 'Yanıt alanı boş bırakılamaz.',
      )
      return
    }
    if (!ticket) return

    setSendingMessage(true)
    setMessageError('')
    setMessageSuccess('')
    try {
      await sendMessage(ticket.id, {
        message: trimmedMessage,
        isInternal: messageMode === 'internal',
      })
      await refreshTicketWorkspace(ticket.id)
      setTimelineRefreshKey((key) => key + 1)
      setMessageText('')
      setMessageSuccess(
        messageMode === 'internal'
          ? 'İç not başarıyla eklendi.'
          : 'Müşteriye yanıt başarıyla gönderildi.',
      )
    } catch (error: unknown) {
      if (messageMode === 'public' && getApiErrorStatus(error) === 502) {
        try {
          await refreshTicketWorkspace(ticket.id)
          setMessageText('')
        } catch {
          // The delivery error remains primary; the next page load will refresh persisted data.
        }
      }
      setMessageError(
        getFriendlyApiErrorMessage(error, {
          fallback: 'Mesaj gönderilemedi. Lütfen tekrar deneyin.',
          forbiddenMessage: 'Bu işlem için yetkiniz bulunmuyor.',
          notFoundMessage: 'Talep bulunamadı.',
          unavailableMessage:
            'SupportFlow hizmetine ulaşılamıyor. Lütfen daha sonra tekrar deneyin.',
        }),
      )
    } finally {
      setSendingMessage(false)
    }
  }

  async function handleRetryDelivery(messageId: number) {
    if (!ticket || retryingMessageId) return

    setRetryingMessageId(messageId)
    setDeliveryRetryError('')
    setMessageSuccess('')

    try {
      await retryMessageDelivery(ticket.id, messageId)
      await refreshTicketWorkspace(ticket.id)
      setMessageSuccess('E-posta müşteriye başarıyla gönderildi.')
    } catch (error: unknown) {
      try {
        await refreshTicketWorkspace(ticket.id)
      } catch {
        // The delivery result remains visible on the next workspace refresh.
      }

      setDeliveryRetryError(
        getApiErrorStatus(error) === 502
          ? 'E-posta gönderilemedi. Mesaj üzerinden daha sonra yeniden deneyebilirsiniz.'
          : getFriendlyApiErrorMessage(error, {
              fallback: 'E-posta yeniden gönderilemedi. Lütfen tekrar deneyin.',
              forbiddenMessage: 'Bu işlem için yetkiniz bulunmuyor.',
              notFoundMessage: 'Mesaj veya talep bulunamadı.',
              unavailableMessage:
                'SupportFlow hizmetine ulaşılamıyor. Lütfen daha sonra tekrar deneyin.',
            }),
      )
    } finally {
      setRetryingMessageId(null)
    }
  }

  function handleAssignmentActionClick() {
    if (!ticket || !assignedUserId) {
      setAssignmentError('Bir teknik personel seçin.')
      return
    }

    if (ticket.assignedUserId?.toString() === assignedUserId) {
      setAssignmentError('Talep zaten seçilen teknik personele atanmış.')
      return
    }

    setAssignmentError('')
    setAssignmentSuccess('')
    setConfirmAssignmentOpen(true)
  }

  async function handleConfirmAssignment() {
    if (!ticket || !assignedUserId) return

    setAssigning(true)
    setAssignmentError('')
    setAssignmentSuccess('')
    try {
      await assignTicket(ticket.id, Number(assignedUserId))
      await refreshTicketWorkspace(ticket.id)
      setTimelineRefreshKey((key) => key + 1)
      setAssignmentSuccess('Talep başarıyla atandı.')
      setConfirmAssignmentOpen(false)
    } catch (error: unknown) {
      setAssignmentError(
        getFriendlyApiErrorMessage(error, {
          fallback: 'Talep atanamadı. Lütfen tekrar deneyin.',
          forbiddenMessage: 'Bu işlem için yetkiniz bulunmuyor.',
          notFoundMessage: 'Talep bulunamadı.',
          unavailableMessage:
            'SupportFlow hizmetine ulaşılamıyor. Lütfen daha sonra tekrar deneyin.',
        }),
      )
    } finally {
      setAssigning(false)
    }
  }

  async function handlePriorityUpdate() {
    if (!ticket || !selectedPriority) return

    if (ticket.priority === selectedPriority) {
      setPriorityError('Talep zaten seçilen önceliğe sahip.')
      return
    }

    setUpdatingPriority(true)
    setPriorityError('')
    setPrioritySuccess('')
    try {
      await updatePriority(ticket.id, selectedPriority)
      await refreshTicketWorkspace(ticket.id)
      setTimelineRefreshKey((key) => key + 1)
      setPrioritySuccess('Talep önceliği başarıyla güncellendi.')
    } catch (error: unknown) {
      setPriorityError(
        getFriendlyApiErrorMessage(error, {
          fallback: 'Talep önceliği güncellenemedi. Lütfen tekrar deneyin.',
          forbiddenMessage: 'Bu işlem için yetkiniz bulunmuyor.',
          notFoundMessage: 'Talep bulunamadı.',
          unavailableMessage:
            'SupportFlow hizmetine ulaşılamıyor. Lütfen daha sonra tekrar deneyin.',
        }),
      )
    } finally {
      setUpdatingPriority(false)
    }
  }

  function handleStatusActionClick() {
    if (!ticket) return

    if (!selectedStatus) {
      setStatusError('Lütfen geçerli bir sonraki durum seçin.')
      return
    }

    setStatusError('')
    setStatusSuccess('')
    setConfirmStatusChangeOpen(true)
  }

  async function handleConfirmStatusUpdate() {
    if (!ticket || !selectedStatus) return

    setUpdatingStatus(true)
    setStatusError('')
    setStatusSuccess('')
    try {
      await updateStatus(ticket.id, {
        status: selectedStatus,
        note: statusNote.trim() || null,
      })
      await refreshTicketWorkspace(ticket.id)
      setTimelineRefreshKey((key) => key + 1)
      setStatusNote('')
      setStatusSuccess('Talep durumu başarıyla güncellendi.')
      setConfirmStatusChangeOpen(false)
    } catch (error: unknown) {
      setStatusError(
        getFriendlyApiErrorMessage(error, {
          fallback: 'Talep durumu güncellenemedi. Lütfen tekrar deneyin.',
          forbiddenMessage: 'Bu işlem için yetkiniz bulunmuyor.',
          notFoundMessage: 'Talep bulunamadı.',
          unavailableMessage:
            'SupportFlow hizmetine ulaşılamıyor. Lütfen daha sonra tekrar deneyin.',
        }),
      )
    } finally {
      setUpdatingStatus(false)
    }
  }

  if (loading) {
    return (
      <Box sx={{ display: 'grid', minHeight: 420, placeItems: 'center' }}>
        <CircularProgress aria-label="Talep ayrıntıları yükleniyor" />
      </Box>
    )
  }

  if (loadError || !ticket) {
    return (
      <Stack spacing={2}>
        <Button
          onClick={() => navigate(ticketsPath)}
          startIcon={<ArrowBack />}
          sx={{ alignSelf: 'flex-start' }}
          variant="outlined"
        >
          Taleplere Dön
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
    <>
      <Stack spacing={3}>
        <Button
          onClick={() => navigate(ticketsPath)}
          startIcon={<ArrowBack />}
          sx={{ alignSelf: 'flex-start' }}
          variant="outlined"
        >
          Taleplere Dön
        </Button>

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
                <RequesterTypeChip requesterType={ticket.requesterType} />
            </Stack>
            <Divider />
            <Stack
              direction={{ xs: 'column', md: 'row' }}
              spacing={2}
              sx={{ color: 'text.secondary', flexWrap: 'wrap' }}
            >
              <Typography variant="body2">Müşteri: {ticket.customerName}</Typography>
              <Typography variant="body2">
                Atanan: {ticket.assignedUserName || 'Atanmadı'}
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
              <Typography sx={{ fontWeight: 700, mb: 1 }} variant="h6">
                Ek Dosyalar
              </Typography>
              <Typography color="text.secondary" sx={{ mb: 2 }} variant="body2">
                Müşterinin talep oluştururken paylaştığı ilk dosyalar burada görüntülenir.
              </Typography>
              {attachmentLoadError ? (
                <Alert severity="error">{attachmentLoadError}</Alert>
              ) : (
                <TicketAttachmentList attachments={attachments} ticketId={ticket.id} />
              )}
            </Paper>

            <Paper sx={{ border: '1px solid', borderColor: 'divider', p: { xs: 2.5, sm: 3 } }}>
              <Typography sx={{ fontWeight: 700, mb: 1 }} variant="h6">
                Konuşma
              </Typography>
              <Typography color="text.secondary" sx={{ mb: 2 }} variant="body2">
                Müşteri mesajları, destek yanıtları ve iç notlar tek akışta görünür.
              </Typography>
              {deliveryRetryError ? <Alert severity="error" sx={{ mb: 2 }}>{deliveryRetryError}</Alert> : null}
              <TicketConversationTimeline
                emptyText="Henüz ek bir mesaj bulunmuyor."
                initialMessage={{
                  senderName: ticket.customerName,
                  message: ticket.description,
                  createdAt: ticket.createdAt,
                  markerLabel: 'İlk Talep',
                }}
                messages={messages}
                onRetryDelivery={(_, messageId) => void handleRetryDelivery(messageId)}
                retryingMessageId={retryingMessageId}
              />
            </Paper>

            <Paper sx={{ border: '1px solid', borderColor: 'divider' }}>
              <Tabs
                onChange={(_, value: MessageMode) => {
                  setMessageMode(value)
                  setMessageText('')
                  setMessageError('')
                  setMessageSuccess('')
                }}
                value={messageMode}
                variant="fullWidth"
              >
                <Tab label="Müşteriye Yanıt" value="public" />
                <Tab label="İç Not" value="internal" />
              </Tabs>
              <Box component="form" noValidate onSubmit={handleMessageSubmit} sx={{ p: 3 }}>
                <Stack spacing={2}>
                  <Alert severity={messageMode === 'internal' ? 'warning' : 'info'}>
                    {messageMode === 'internal'
                      ? 'Bu not müşteriye gösterilmez.'
                      : 'Bu yanıt müşterinin talep ekranında görünür.'}
                  </Alert>
                  {messageSuccess && <Alert severity="success">{messageSuccess}</Alert>}
                  {messageError && <Alert severity="error">{messageError}</Alert>}
                  <TextField
                    disabled={sendingMessage}
                    error={messageError.endsWith('boş bırakılamaz.')}
                    label={messageMode === 'internal' ? 'İç Not' : 'Müşteriye Yanıt'}
                    minRows={5}
                    multiline
                    onChange={(event) => {
                      setMessageText(event.target.value)
                      setMessageError('')
                      setMessageSuccess('')
                    }}
                    required
                    value={messageText}
                  />
                  <Box sx={{ display: 'flex', justifyContent: 'flex-end' }}>
                    <Button
                      color={messageMode === 'internal' ? 'warning' : 'primary'}
                      disabled={sendingMessage}
                      startIcon={
                        sendingMessage ? (
                          <CircularProgress aria-hidden="true" color="inherit" size={18} />
                        ) : messageMode === 'internal' ? (
                          <LockOutlined />
                        ) : (
                          <Send />
                        )
                      }
                      type="submit"
                      variant="contained"
                    >
                      {sendingMessage
                        ? 'Gönderiliyor...'
                        : messageMode === 'internal'
                          ? 'İç Not Ekle'
                          : 'Yanıtı Gönder'}
                    </Button>
                  </Box>
                </Stack>
              </Box>
            </Paper>

            <TicketActivityTimeline
              key={`${ticket.id}-${timelineRefreshKey}`}
              role={routeBase === '/admin' ? UserRole.Admin : UserRole.TechnicalStaff}
              ticketId={ticket.id}
            />
          </Stack>

          <Stack spacing={3}>
            <Paper sx={{ border: '1px solid', borderColor: 'divider', p: 3 }}>
              <Typography sx={{ fontWeight: 700, mb: 2 }} variant="h6">
                Talep Bilgileri
              </Typography>
              <Stack spacing={1.4}>
                <Typography variant="body2">
                  <strong>Müşteri:</strong> {ticket.customerName}
                </Typography>
                <Typography variant="body2">
                  <strong>E-posta:</strong> {ticket.customerEmail}
                </Typography>
                <Typography color="text.secondary" variant="caption">
                  {ticket.requesterType === 'ExternalRequester'
                    ? 'Bu talep harici e-posta göndericisinden alındı.'
                    : 'Bu talep kayıtlı müşteri hesabına bağlı.'}
                </Typography>
                <Typography variant="body2">
                  <strong>Mevcut Durum:</strong> {getTicketStatusLabel(ticket.status)}
                </Typography>
                <Typography variant="body2">
                  <strong>Öncelik:</strong> {getTicketPriorityLabel(ticket.priority)}
                </Typography>
                <Typography variant="body2">
                  <strong>Atanan Personel:</strong> {ticket.assignedUserName || 'Atanmadı'}
                </Typography>
                <Typography variant="body2">
                  <strong>Oluşturulma:</strong> {formatDate(ticket.createdAt)}
                </Typography>
                <Typography variant="body2">
                  <strong>Son Güncelleme:</strong> {formatDate(ticket.updatedAt)}
                </Typography>
              </Stack>
            </Paper>

            <TicketSlaPanel sla={ticket.sla} />

            <TicketClassificationCard
              editable
              onSaved={(classification) => {
                setTicket((currentTicket) => currentTicket
                  ? { ...currentTicket, category: classification.category, tags: classification.tags }
                  : currentTicket)
                setTimelineRefreshKey((key) => key + 1)
              }}
              ticket={ticket}
            />

            <Paper sx={{ border: '1px solid', borderColor: 'divider', p: 3 }}>
              <Typography sx={{ fontWeight: 700, mb: 2 }} variant="h6">
                Atama
              </Typography>
              {assignmentSuccess && (
                <Alert severity="success" sx={{ mb: 2 }}>
                  {assignmentSuccess}
                </Alert>
              )}
              {assignmentError && (
                <Alert severity="error" sx={{ mb: 2 }}>
                  {assignmentError}
                </Alert>
              )}
              <FormControl fullWidth error={Boolean(assignmentError && !assignedUserId)}>
                <InputLabel id="technical-staff-label">Teknik Personel</InputLabel>
                <Select
                  label="Teknik Personel"
                  labelId="technical-staff-label"
                  onChange={(event: SelectChangeEvent) => {
                    setAssignedUserId(event.target.value)
                    setAssignmentError('')
                    setAssignmentSuccess('')
                  }}
                  value={assignedUserId}
                >
                  {technicalStaff.map((staff) => (
                    <MenuItem key={staff.id} value={staff.id.toString()}>
                      {staff.firstName} {staff.lastName}
                    </MenuItem>
                  ))}
                </Select>
                {technicalStaff.length === 0 && (
                  <FormHelperText>Aktif teknik personel bulunamadı.</FormHelperText>
                )}
              </FormControl>
              <Button
                disabled={assigning || technicalStaff.length === 0}
                fullWidth
                onClick={handleAssignmentActionClick}
                sx={{ mt: 2 }}
                variant="contained"
              >
                {assigning ? 'Atanıyor...' : 'Atamayı Kaydet'}
              </Button>
            </Paper>

            <Paper sx={{ border: '1px solid', borderColor: 'divider', p: 3 }}>
              <Typography sx={{ fontWeight: 700, mb: 2 }} variant="h6">
                Öncelik
              </Typography>
              {prioritySuccess && (
                <Alert severity="success" sx={{ mb: 2 }}>
                  {prioritySuccess}
                </Alert>
              )}
              {priorityError && (
                <Alert severity="error" sx={{ mb: 2 }}>
                  {priorityError}
                </Alert>
              )}
              <Stack spacing={2}>
                <FormControl fullWidth>
                  <InputLabel id="ticket-priority-label">Öncelik</InputLabel>
                  <Select
                    label="Öncelik"
                    labelId="ticket-priority-label"
                    onChange={(event: SelectChangeEvent) => {
                      setSelectedPriority(event.target.value as TicketPriority)
                      setPriorityError('')
                      setPrioritySuccess('')
                    }}
                    value={selectedPriority}
                  >
                    {Object.values(TicketPriority).map((priority) => (
                      <MenuItem key={priority} value={priority}>
                        {getTicketPriorityLabel(priority)}
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>
                <Button
                  disabled={updatingPriority || !selectedPriority || selectedPriority === ticket.priority}
                  onClick={() => void handlePriorityUpdate()}
                  variant="contained"
                >
                  {updatingPriority ? 'Güncelleniyor...' : 'Önceliği Güncelle'}
                </Button>
              </Stack>
            </Paper>

            <Paper sx={{ border: '1px solid', borderColor: 'divider', p: 3 }}>
              <Typography sx={{ fontWeight: 700, mb: 2 }} variant="h6">
                Durum Değişikliği
              </Typography>
              {statusSuccess && (
                <Alert severity="success" sx={{ mb: 2 }}>
                  {statusSuccess}
                </Alert>
              )}
              {statusError && (
                <Alert severity="error" sx={{ mb: 2 }}>
                  {statusError}
                </Alert>
              )}
              <Stack spacing={2}>
                <FormControl fullWidth>
                  <InputLabel id="ticket-status-label">Sonraki Durum</InputLabel>
                  <Select
                    disabled={allowedNextStatuses.length === 0}
                    label="Sonraki Durum"
                    labelId="ticket-status-label"
                    onChange={(event: SelectChangeEvent) => {
                      setSelectedStatus(event.target.value as StatusSelection)
                      setStatusError('')
                      setStatusSuccess('')
                    }}
                    value={selectedStatus}
                  >
                    {allowedNextStatuses.map((status) => (
                      <MenuItem key={status} value={status}>
                        {getTicketStatusLabel(status)}
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>
                {allowedNextStatuses.length === 0 && (
                  <Alert severity="info">
                    Bu talep için seçilebilir bir sonraki durum bulunmuyor.
                  </Alert>
                )}
                <TextField
                  label="Durum Notu"
                  minRows={3}
                  multiline
                  onChange={(event) => setStatusNote(event.target.value)}
                  placeholder="İsteğe bağlı"
                  value={statusNote}
                />
                <Button
                  disabled={updatingStatus || !selectedStatus}
                  onClick={handleStatusActionClick}
                  variant="contained"
                >
                  {updatingStatus ? 'Güncelleniyor...' : 'Durumu Güncelle'}
                </Button>
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
      </Stack>

      <Dialog
        fullWidth
        maxWidth="sm"
        onClose={() => {
          if (!assigning) {
            setConfirmAssignmentOpen(false)
          }
        }}
        open={confirmAssignmentOpen}
      >
        <DialogTitle>Atamayı Onayla</DialogTitle>
        <DialogContent dividers>
          <Stack spacing={1.5}>
            <Typography>
              Bu talebi <strong>{selectedAssigneeName}</strong> kullanıcısına atamak istediğinize
              emin misiniz?
            </Typography>
            <Typography>
              <strong>Talep No:</strong> {ticket.ticketNumber}
            </Typography>
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button
            disabled={assigning}
            onClick={() => setConfirmAssignmentOpen(false)}
            variant="outlined"
          >
            Vazgeç
          </Button>
          <Button
            disabled={assigning}
            onClick={() => void handleConfirmAssignment()}
            variant="contained"
          >
            {assigning ? 'Atanıyor...' : 'Onayla'}
          </Button>
        </DialogActions>
      </Dialog>

      <Dialog
        fullWidth
        maxWidth="sm"
        onClose={() => {
          if (!updatingStatus) {
            setConfirmStatusChangeOpen(false)
          }
        }}
        open={confirmStatusChangeOpen}
      >
        <DialogTitle>Durum Değişikliğini Onayla</DialogTitle>
        <DialogContent dividers>
          <Stack spacing={1.5}>
            <Typography>
              <strong>Talep No:</strong> {ticket.ticketNumber}
            </Typography>
            <Typography>
              <strong>Mevcut Durum:</strong> {getTicketStatusLabel(ticket.status)}
            </Typography>
            <Typography>
              <strong>Hedef Durum:</strong>{' '}
              {selectedStatus ? getTicketStatusLabel(selectedStatus) : 'Seçilmedi'}
            </Typography>
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button
            disabled={updatingStatus}
            onClick={() => setConfirmStatusChangeOpen(false)}
            variant="outlined"
          >
            Vazgeç
          </Button>
          <Button
            disabled={updatingStatus || !selectedStatus}
            onClick={() => void handleConfirmStatusUpdate()}
            variant="contained"
          >
            {updatingStatus ? 'Güncelleniyor...' : 'Onayla'}
          </Button>
        </DialogActions>
      </Dialog>
    </>
  )
}
