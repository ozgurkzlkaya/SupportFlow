import { ArrowBack, EditNoteOutlined, Send } from '@mui/icons-material'
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  FormControl,
  FormHelperText,
  InputLabel,
  MenuItem,
  Paper,
  Select,
  Stack,
  TextField,
  Typography,
  type SelectChangeEvent,
} from '@mui/material'
import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import supportflowIllustration from '../../assets/brand/supportflow-auth-illustration.svg'
import { TicketAttachmentPicker } from '../../components/common/TicketAttachmentPicker'
import { PageHeader } from '../../components/common/PageHeader'
import { uploadTicketAttachment } from '../../services/attachmentService'
import { createTicket } from '../../services/ticketService'
import { brandGradients } from '../../theme'
import { TicketPriority } from '../../types/ticket'
import { getFriendlyApiErrorMessage } from '../../utils/getApiErrorMessage'
import { getTicketPriorityLabel } from '../../utils/ticketLabels'

const priorityOptions = Object.values(TicketPriority)
const maxAttachmentCount = 5
const maxAttachmentSizeBytes = 5 * 1024 * 1024
const allowedAttachmentTypes = new Set([
  'image/png',
  'image/jpeg',
  'image/webp',
  'application/pdf',
])

export function CreateTicketPage() {
  const navigate = useNavigate()
  const [title, setTitle] = useState('')
  const [description, setDescription] = useState('')
  const [priority, setPriority] = useState<TicketPriority>(TicketPriority.Normal)
  const [selectedFiles, setSelectedFiles] = useState<File[]>([])
  const [titleError, setTitleError] = useState('')
  const [descriptionError, setDescriptionError] = useState('')
  const [attachmentError, setAttachmentError] = useState('')
  const [submitError, setSubmitError] = useState('')
  const [submitting, setSubmitting] = useState(false)

  function validateAttachmentBatch(nextFiles: File[]): string {
    if (nextFiles.length > maxAttachmentCount) {
      return 'En fazla 5 dosya ekleyebilirsiniz.'
    }

    const invalidTypeFile = nextFiles.find((file) => !allowedAttachmentTypes.has(file.type))
    if (invalidTypeFile) {
      return `İzin verilmeyen dosya türü: ${invalidTypeFile.name}`
    }

    const oversizedFile = nextFiles.find((file) => file.size > maxAttachmentSizeBytes)
    if (oversizedFile) {
      return `5 MB sınırını aşan dosya: ${oversizedFile.name}`
    }

    return ''
  }

  function handleAddFiles(files: File[]) {
    const mergedFiles = [...selectedFiles, ...files].slice(0, maxAttachmentCount)
    const validationError =
      files.length + selectedFiles.length > maxAttachmentCount
        ? 'En fazla 5 dosya ekleyebilirsiniz.'
        : validateAttachmentBatch(mergedFiles)

    setAttachmentError(validationError)

    if (!validationError) {
      setSelectedFiles(mergedFiles)
    }
  }

  function handleRemoveFile(index: number) {
    setSelectedFiles((currentFiles) => currentFiles.filter((_, fileIndex) => fileIndex !== index))
    setAttachmentError('')
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    const trimmedTitle = title.trim()
    const trimmedDescription = description.trim()
    const nextTitleError = !trimmedTitle
      ? 'Konu alanı gereklidir.'
      : trimmedTitle.length > 250
        ? 'Konu en fazla 250 karakter olabilir.'
        : ''
    const nextDescriptionError = trimmedDescription ? '' : 'Açıklama alanı gereklidir.'
    const nextAttachmentError = validateAttachmentBatch(selectedFiles)

    setTitleError(nextTitleError)
    setDescriptionError(nextDescriptionError)
    setAttachmentError(nextAttachmentError)
    setSubmitError('')

    if (nextTitleError || nextDescriptionError || nextAttachmentError) {
      return
    }

    setSubmitting(true)

    try {
      const createdTicket = await createTicket({
        title: trimmedTitle,
        description: trimmedDescription,
        priority,
      })

      const failedAttachmentNames: string[] = []

      for (const file of selectedFiles) {
        try {
          await uploadTicketAttachment(createdTicket.id, file)
        } catch {
          failedAttachmentNames.push(file.name)
        }
      }

      navigate(`/customer/tickets/${createdTicket.id}`, {
        replace: true,
        state: {
          ticketCreated: true,
          uploadedAttachmentCount: selectedFiles.length - failedAttachmentNames.length,
          failedAttachmentNames,
        },
      })
    } catch (error: unknown) {
      setSubmitError(
        getFriendlyApiErrorMessage(error, {
          fallback: 'Talebiniz oluşturulamadı. Lütfen tekrar deneyin.',
          forbiddenMessage: 'Bu işlem için yetkiniz bulunmuyor.',
          unavailableMessage:
            'SupportFlow hizmetine ulaşılamıyor. Lütfen daha sonra tekrar deneyin.',
        }),
      )
    } finally {
      setSubmitting(false)
    }
  }

  function handlePriorityChange(event: SelectChangeEvent) {
    setPriority(event.target.value as TicketPriority)
  }

  return (
    <Stack spacing={3}>
      <PageHeader
        actions={
          <Button
            onClick={() => navigate('/customer/tickets')}
            startIcon={<ArrowBack />}
            variant="outlined"
          >
            Taleplere Dön
          </Button>
        }
        description="Sorununuzu açık ve net şekilde iletin. Talep oluşturulduktan sonra aynı kayıt altında yazışmaya devam edebilirsiniz."
        eyebrow="Yeni Kayıt"
        title="Yeni Destek Talebi"
      />

      <Box
        sx={{
          display: 'grid',
          gap: 3,
          gridTemplateColumns: { xs: '1fr', xl: 'minmax(0, 1.15fr) minmax(320px, 0.85fr)' },
        }}
      >
        <Paper sx={{ border: '1px solid', borderColor: 'divider', p: { xs: 2.5, sm: 3.5 } }}>
          <Box component="form" noValidate onSubmit={handleSubmit}>
            <Stack spacing={3}>
              {submitError && <Alert severity="error">{submitError}</Alert>}

              <TextField
                autoFocus
                error={Boolean(titleError)}
                helperText={titleError || `${title.length}/250`}
                label="Konu"
                name="title"
                onChange={(event) => {
                  setTitle(event.target.value)
                  setTitleError('')
                  setSubmitError('')
                }}
                required
                slotProps={{ htmlInput: { maxLength: 250 } }}
                value={title}
              />

              <TextField
                error={Boolean(descriptionError)}
                helperText={descriptionError || 'Sorununuzu mümkün olduğu kadar ayrıntılı açıklayın.'}
                label="Açıklama"
                minRows={8}
                multiline
                name="description"
                onChange={(event) => {
                  setDescription(event.target.value)
                  setDescriptionError('')
                  setSubmitError('')
                }}
                required
                value={description}
              />

              <FormControl>
                <InputLabel id="priority-label">Öncelik</InputLabel>
                <Select
                  label="Öncelik"
                  labelId="priority-label"
                  name="priority"
                  onChange={handlePriorityChange}
                  value={priority}
                >
                  {priorityOptions.map((option) => (
                    <MenuItem key={option} value={option}>
                      {getTicketPriorityLabel(option)}
                    </MenuItem>
                  ))}
                </Select>
                <FormHelperText>
                  Talebinizin iş etkisine göre uygun öncelik seviyesini seçin.
                </FormHelperText>
              </FormControl>

              <TicketAttachmentPicker
                error={attachmentError}
                files={selectedFiles}
                onAddFiles={handleAddFiles}
                onRemoveFile={handleRemoveFile}
              />

              <Stack
                direction={{ xs: 'column-reverse', sm: 'row' }}
                spacing={1.5}
                sx={{ justifyContent: 'flex-end' }}
              >
                <Button
                  disabled={submitting}
                  onClick={() => navigate('/customer/tickets')}
                  variant="outlined"
                >
                  İptal
                </Button>
                <Button
                  disabled={submitting}
                  startIcon={
                    submitting ? (
                      <CircularProgress aria-hidden="true" color="inherit" size={18} />
                    ) : (
                      <Send />
                    )
                  }
                  type="submit"
                  variant="contained"
                >
                  {submitting
                    ? selectedFiles.length > 0
                      ? 'Talep ve dosyalar gönderiliyor...'
                      : 'Talep oluşturuluyor...'
                    : 'Talebi Oluştur'}
                </Button>
              </Stack>
            </Stack>
          </Box>
        </Paper>

        <Stack spacing={3}>
          <Paper
            sx={{
              background: brandGradients.guidance,
              color: '#fff',
              overflow: 'hidden',
              p: 3,
              position: 'relative',
            }}
          >
            <Stack spacing={2} sx={{ maxWidth: 320, position: 'relative', zIndex: 1 }}>
              <EditNoteOutlined />
              <Typography sx={{ fontWeight: 800 }} variant="h5">
                Etkili talep nasıl yazılır?
              </Typography>
              <Typography sx={{ color: 'rgba(255,255,255,0.82)' }} variant="body2">
                Sorunun ne zaman başladığını, hangi ekranda oluştuğunu ve işinize etkisini
                belirtmek çözüm süresini kısaltır.
              </Typography>
            </Stack>
            <Box
              alt="SupportFlow destek görseli"
              component="img"
              src={supportflowIllustration}
              sx={{
                bottom: -14,
                maxWidth: 280,
                opacity: 0.28,
                position: 'absolute',
                right: -28,
                width: '58%',
              }}
            />
          </Paper>

          <Paper sx={{ border: '1px solid', borderColor: 'divider', p: 3 }}>
            <Stack spacing={1.5}>
              <Typography sx={{ fontWeight: 700 }} variant="h6">
                Talep açıldıktan sonra
              </Typography>
              <Typography color="text.secondary" variant="body2">
                Talebiniz sisteme düştükten sonra destek ekibi aynı kayıt altında size yanıt
                verebilir. Siz de aynı talep üzerinden sürece devam edebilirsiniz.
              </Typography>
            </Stack>
          </Paper>
        </Stack>
      </Box>
    </Stack>
  )
}
