import { SendOutlined } from '@mui/icons-material'
import { Alert, Box, Button, CircularProgress, Stack, TextField, Typography } from '@mui/material'
import { useRef, useState, type FormEvent } from 'react'
import { sendMessage } from '../../services/ticketService'
import type { ConversationTicket } from '../../types/conversation'
import { getApiErrorStatus, getFriendlyApiErrorMessage } from '../../utils/getApiErrorMessage'

interface PublicReplyComposerProps {
  ticket: ConversationTicket
  onMessagePersisted: () => Promise<void>
  onReplySucceeded: () => void
  onReplyDeliveryFailed: () => void
}

export function PublicReplyComposer({
  ticket,
  onMessagePersisted,
  onReplySucceeded,
  onReplyDeliveryFailed,
}: PublicReplyComposerProps) {
  const [message, setMessage] = useState('')
  const [error, setError] = useState('')
  const [warning, setWarning] = useState('')
  const [sending, setSending] = useState(false)
  const sendingRef = useRef(false)

  async function refreshConversation() {
    try {
      await onMessagePersisted()
      return true
    } catch {
      return false
    }
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const trimmedMessage = message.trim()

    if (!trimmedMessage) {
      setError('Yanıt alanı boş bırakılamaz.')
      return
    }

    if (sendingRef.current) return

    sendingRef.current = true
    setSending(true)
    setError('')
    setWarning('')

    try {
      await sendMessage(ticket.id, { isInternal: false, message: trimmedMessage })
      const refreshed = await refreshConversation()
      setMessage('')
      if (refreshed) {
        onReplySucceeded()
      } else {
        setWarning('Yanıt gönderildi ancak görüşme görünümü yenilenemedi.')
      }
    } catch (requestError: unknown) {
      if (getApiErrorStatus(requestError) === 502) {
        const refreshed = await refreshConversation()
        setMessage('')
        if (refreshed) {
          onReplyDeliveryFailed()
        } else {
          setWarning('Yanıt talebe kaydedildi ancak e-posta gönderilemedi. Mesaj üzerinden yeniden deneyebilirsiniz.')
        }
      } else {
        setError(
          getFriendlyApiErrorMessage(requestError, {
            fallback: 'Yanıt gönderilemedi. Lütfen tekrar deneyin.',
            forbiddenMessage: 'Bu işlem için yetkiniz bulunmuyor.',
            notFoundMessage: 'Talep bulunamadı.',
            unavailableMessage:
              'SupportFlow hizmetine ulaşılamıyor. Lütfen daha sonra tekrar deneyin.',
          }),
        )
      }
    } finally {
      sendingRef.current = false
      setSending(false)
    }
  }

  return (
    <Box
      component="form"
      noValidate
      onSubmit={handleSubmit}
      sx={{ border: '1px solid', borderColor: 'primary.light', borderRadius: 2.5, p: { xs: 1.25, sm: 1.5 } }}
    >
      <Stack spacing={1.25}>
        <Box>
          <Typography sx={{ fontWeight: 800 }} variant="subtitle2">
            Müşteriye Yanıt
          </Typography>
          <Typography color="text.secondary" variant="caption">
            Yanıtlanan Talep: {ticket.ticketNumber} — {ticket.title}
          </Typography>
        </Box>
        {warning ? <Alert severity="warning">{warning}</Alert> : null}
        {error ? <Alert severity="error">{error}</Alert> : null}
        <TextField
          disabled={sending}
          error={Boolean(error)}
          label="Yanıt"
          minRows={3}
          multiline
          onChange={(event) => {
            setMessage(event.target.value)
            setError('')
            setWarning('')
          }}
          placeholder="Müşteriye gönderilecek public yanıtı yazın."
          required
          value={message}
        />
        <Box sx={{ display: 'flex', justifyContent: 'flex-end' }}>
          <Button
            disabled={sending || !message.trim()}
            startIcon={
              sending ? <CircularProgress aria-hidden="true" color="inherit" size={18} /> : <SendOutlined />
            }
            sx={{ width: { xs: '100%', sm: 'auto' } }}
            type="submit"
            variant="contained"
          >
            {sending ? 'Gönderiliyor...' : 'Yanıt Gönder'}
          </Button>
        </Box>
      </Stack>
    </Box>
  )
}
