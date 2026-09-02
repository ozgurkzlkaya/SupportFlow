import {
  CloseOutlined,
  DescriptionOutlined,
  DownloadOutlined,
  ImageOutlined,
  OpenInNewOutlined,
} from '@mui/icons-material'
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Dialog,
  DialogContent,
  DialogTitle,
  IconButton,
  Paper,
  Stack,
  Typography,
} from '@mui/material'
import { useEffect, useMemo, useState } from 'react'
import { downloadTicketAttachment } from '../../services/attachmentService'
import type { TicketAttachment } from '../../types/attachment'
import { formatDate } from '../../utils/formatDate'
import { formatFileSize } from '../../utils/formatFileSize'

interface TicketAttachmentListProps {
  ticketId: number
  attachments: TicketAttachment[]
}

interface PreviewDialogState {
  attachment: TicketAttachment
  error: string
  imageLoading: boolean
  requestLoading: boolean
  url: string | null
}

function isImageBlob(blob: Blob) {
  return blob.size > 0 && blob.type.startsWith('image/')
}

export function TicketAttachmentList({
  ticketId,
  attachments,
}: TicketAttachmentListProps) {
  const orderedAttachments = useMemo(
    () =>
      [...attachments].sort(
        (first, second) =>
          new Date(second.createdAt).getTime() - new Date(first.createdAt).getTime(),
      ),
    [attachments],
  )
  const [previewUrls, setPreviewUrls] = useState<Record<number, string>>({})
  const [thumbnailLoadErrors, setThumbnailLoadErrors] = useState<Record<number, boolean>>({})
  const [actionError, setActionError] = useState('')
  const [previewDialog, setPreviewDialog] = useState<PreviewDialogState | null>(null)
  const previewAttachment = previewDialog?.attachment ?? null

  useEffect(() => {
    let active = true
    const allocatedUrls: string[] = []

    setThumbnailLoadErrors({})

    async function loadPreviews() {
      const imageAttachments = orderedAttachments.filter((attachment) => attachment.isImage)
      const nextPreviewUrls: Record<number, string> = {}

      for (const attachment of imageAttachments) {
        try {
          const blob = await downloadTicketAttachment(ticketId, attachment.id)

          if (!isImageBlob(blob)) {
            nextPreviewUrls[attachment.id] = ''
            continue
          }

          const url = URL.createObjectURL(blob)
          allocatedUrls.push(url)
          nextPreviewUrls[attachment.id] = url
        } catch {
          nextPreviewUrls[attachment.id] = ''
        }
      }

      if (active) {
        setPreviewUrls(nextPreviewUrls)
      }
    }

    void loadPreviews()

    return () => {
      active = false
      allocatedUrls.forEach((url) => URL.revokeObjectURL(url))
    }
  }, [orderedAttachments, ticketId])

  useEffect(() => {
    if (!previewAttachment?.isImage) {
      return
    }

    const attachment = previewAttachment
    let active = true
    let previewUrl: string | null = null

    async function loadPreview() {
      setPreviewDialog((currentPreview) =>
        currentPreview
          ? {
              ...currentPreview,
              error: '',
              imageLoading: false,
              requestLoading: true,
              url: null,
            }
          : currentPreview,
      )

      try {
        const blob = await downloadTicketAttachment(ticketId, attachment.id)

        if (!isImageBlob(blob)) {
          throw new Error('invalid-image-blob')
        }

        previewUrl = URL.createObjectURL(blob)

        if (!active) {
          URL.revokeObjectURL(previewUrl)
          return
        }

        setPreviewDialog((currentPreview) =>
          currentPreview
            ? {
                ...currentPreview,
                error: '',
                imageLoading: true,
                requestLoading: false,
                url: previewUrl,
              }
            : currentPreview,
        )
      } catch {
        if (!active) {
          return
        }

        setPreviewDialog((currentPreview) =>
          currentPreview
            ? {
                ...currentPreview,
                error: 'Görsel önizlenemedi.',
                imageLoading: false,
                requestLoading: false,
                url: null,
              }
            : currentPreview,
        )
      }
    }

    void loadPreview()

    return () => {
      active = false

      if (previewUrl) {
        URL.revokeObjectURL(previewUrl)
      }
    }
  }, [previewAttachment, ticketId])

  const dialogLoading = useMemo(
    () => Boolean(previewDialog?.requestLoading || previewDialog?.imageLoading),
    [previewDialog?.imageLoading, previewDialog?.requestLoading],
  )

  async function openAttachment(attachment: TicketAttachment) {
    setActionError('')

    try {
      if (attachment.isImage) {
        setPreviewDialog({
          attachment,
          error: '',
          imageLoading: false,
          requestLoading: true,
          url: null,
        })
        return
      }

      const blob = await downloadTicketAttachment(ticketId, attachment.id)
      const url = URL.createObjectURL(blob)

      if (attachment.contentType === 'application/pdf') {
        window.open(url, '_blank', 'noopener,noreferrer')
        window.setTimeout(() => URL.revokeObjectURL(url), 60_000)
        return
      }

      const link = document.createElement('a')
      link.href = url
      link.download = attachment.fileName
      link.target = '_blank'
      link.rel = 'noopener noreferrer'
      link.click()
      window.setTimeout(() => URL.revokeObjectURL(url), 60_000)
    } catch {
      setActionError('Ek dosya açılamadı. Lütfen tekrar deneyin.')
    }
  }

  function handleClosePreviewDialog() {
    setPreviewDialog(null)
  }

  function handleDialogImageLoad() {
    setPreviewDialog((currentPreview) =>
      currentPreview
        ? {
            ...currentPreview,
            error: '',
            imageLoading: false,
          }
        : currentPreview,
    )
  }

  function handleDialogImageError() {
    setPreviewDialog((currentPreview) =>
      currentPreview
        ? {
            ...currentPreview,
            error: 'Görsel önizlenemedi.',
            imageLoading: false,
          }
        : currentPreview,
    )
  }

  if (orderedAttachments.length === 0) {
    return (
      <Paper sx={{ border: '1px solid', borderColor: 'divider', p: 2.5 }}>
        <Typography color="text.secondary" variant="body2">
          Bu talep için henüz ek dosya bulunmuyor.
        </Typography>
      </Paper>
    )
  }

  return (
    <>
      <Stack spacing={1.5}>
        {actionError ? <Alert severity="error">{actionError}</Alert> : null}
        <Box
          sx={{
            display: 'grid',
            gap: 1.25,
            gridTemplateColumns: {
              xs: '1fr',
              md: 'repeat(2, minmax(0, 1fr))',
            },
          }}
        >
          {orderedAttachments.map((attachment) => {
            const previewUrl = previewUrls[attachment.id]
            const thumbnailBroken = Boolean(thumbnailLoadErrors[attachment.id])

            return (
              <Paper
                key={attachment.id}
                sx={{
                  border: '1px solid',
                  borderColor: 'divider',
                  p: 1.5,
                }}
              >
                <Stack spacing={1.15}>
                  <Stack direction="row" spacing={1.2} sx={{ alignItems: 'center' }}>
                    <Box
                      sx={{
                        alignItems: 'center',
                        bgcolor: 'rgba(124, 58, 237, 0.08)',
                        borderRadius: 2,
                        color: 'primary.main',
                        display: 'grid',
                        flexShrink: 0,
                        height: 62,
                        overflow: 'hidden',
                        placeItems: 'center',
                        width: 62,
                      }}
                    >
                      {attachment.isImage && previewUrl && !thumbnailBroken ? (
                        <Box
                          alt={attachment.fileName}
                          component="img"
                          onError={() =>
                            setThumbnailLoadErrors((currentErrors) => ({
                              ...currentErrors,
                              [attachment.id]: true,
                            }))
                          }
                          src={previewUrl}
                          sx={{
                            display: 'block',
                            height: '100%',
                            objectFit: 'cover',
                            width: '100%',
                          }}
                        />
                      ) : attachment.contentType === 'application/pdf' ? (
                        <DescriptionOutlined />
                      ) : (
                        <ImageOutlined />
                      )}
                    </Box>
                    <Box sx={{ flexGrow: 1, minWidth: 0 }}>
                      <Typography noWrap sx={{ fontWeight: 700 }} variant="body2">
                        {attachment.fileName}
                      </Typography>
                      <Typography color="text.secondary" variant="caption">
                        {formatFileSize(attachment.fileSize)} • {attachment.contentType}
                      </Typography>
                      <Typography
                        color="text.secondary"
                        sx={{ display: 'block', mt: 0.5 }}
                        variant="caption"
                      >
                        Yükleyen: {attachment.uploadedByUserName} •{' '}
                        {formatDate(attachment.createdAt)}
                      </Typography>
                    </Box>
                  </Stack>

                  <Button
                    onClick={() => void openAttachment(attachment)}
                    size="small"
                    startIcon={
                      attachment.isImage ? <OpenInNewOutlined /> : <DownloadOutlined />
                    }
                    variant="outlined"
                  >
                    {attachment.isImage
                      ? 'Görseli Önizle'
                      : attachment.contentType === 'application/pdf'
                        ? "PDF'yi Aç"
                        : 'İndir'}
                  </Button>
                </Stack>
              </Paper>
            )
          })}
        </Box>
      </Stack>

      <Dialog
        fullWidth
        maxWidth="md"
        onClose={handleClosePreviewDialog}
        open={previewDialog !== null}
      >
        <DialogTitle
          sx={{
            alignItems: 'center',
            display: 'flex',
            justifyContent: 'space-between',
            gap: 2,
          }}
        >
          <Typography noWrap sx={{ fontWeight: 700 }} variant="h6">
            {previewDialog?.attachment.fileName}
          </Typography>
          <IconButton aria-label="Önizlemeyi kapat" onClick={handleClosePreviewDialog}>
            <CloseOutlined />
          </IconButton>
        </DialogTitle>
        <DialogContent
          dividers
          sx={{
            bgcolor: 'rgba(15, 23, 42, 0.02)',
            minHeight: 280,
            p: 2,
          }}
        >
          {previewDialog?.error ? (
            <Alert severity="error">{previewDialog.error}</Alert>
          ) : null}

          {dialogLoading ? (
            <Box sx={{ display: 'grid', minHeight: 220, placeItems: 'center' }}>
              <CircularProgress aria-label="Görsel önizlemesi yükleniyor" />
            </Box>
          ) : null}

          {previewDialog?.url && !previewDialog.error ? (
            <Box
              alt={previewDialog.attachment.fileName}
              component="img"
              onError={handleDialogImageError}
              onLoad={handleDialogImageLoad}
              src={previewDialog.url}
              sx={{
                display: dialogLoading ? 'none' : 'block',
                height: 'auto',
                maxHeight: '70vh',
                maxWidth: '100%',
                mx: 'auto',
                objectFit: 'contain',
                width: 'auto',
              }}
            />
          ) : null}
        </DialogContent>
      </Dialog>
    </>
  )
}
