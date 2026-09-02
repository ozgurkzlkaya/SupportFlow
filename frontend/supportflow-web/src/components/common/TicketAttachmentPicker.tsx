import {
  CloudUploadOutlined,
  DeleteOutlined,
  DescriptionOutlined,
  ImageOutlined,
} from '@mui/icons-material'
import {
  Box,
  Button,
  IconButton,
  Paper,
  Stack,
  Typography,
} from '@mui/material'
import { useEffect, useMemo, useRef, useState, type DragEvent } from 'react'
import { formatFileSize } from '../../utils/formatFileSize'

interface TicketAttachmentPickerProps {
  files: File[]
  error?: string
  onAddFiles: (files: File[]) => void
  onRemoveFile: (index: number) => void
}

const acceptedTypesLabel = 'PNG, JPG, WEBP, PDF'

export function TicketAttachmentPicker({
  files,
  error,
  onAddFiles,
  onRemoveFile,
}: TicketAttachmentPickerProps) {
  const inputRef = useRef<HTMLInputElement | null>(null)
  const [dragActive, setDragActive] = useState(false)
  const [previewUrls, setPreviewUrls] = useState<Record<string, string>>({})

  useEffect(() => {
    const nextUrls: Record<string, string> = {}

    files.forEach((file) => {
      if (file.type.startsWith('image/')) {
        nextUrls[file.name + file.lastModified] = URL.createObjectURL(file)
      }
    })

    setPreviewUrls(nextUrls)

    return () => {
      Object.values(nextUrls).forEach((url) => URL.revokeObjectURL(url))
    }
  }, [files])

  const fileCards = useMemo(
    () =>
      files.map((file, index) => {
        const key = file.name + file.lastModified
        const previewUrl = previewUrls[key]

        return (
          <Paper
            key={key}
            sx={{
              border: '1px solid',
              borderColor: 'divider',
              p: 1.5,
            }}
          >
            <Stack direction="row" spacing={1.25} sx={{ alignItems: 'center' }}>
              <Box
                sx={{
                  alignItems: 'center',
                  bgcolor: 'rgba(124, 58, 237, 0.08)',
                  borderRadius: 2,
                  color: 'primary.main',
                  display: 'grid',
                  flexShrink: 0,
                  height: 56,
                  overflow: 'hidden',
                  placeItems: 'center',
                  width: 56,
                }}
              >
                {previewUrl ? (
                  <Box
                    alt={file.name}
                    component="img"
                    src={previewUrl}
                    sx={{ display: 'block', height: '100%', objectFit: 'cover', width: '100%' }}
                  />
                ) : file.type === 'application/pdf' ? (
                  <DescriptionOutlined />
                ) : (
                  <ImageOutlined />
                )}
              </Box>
              <Box sx={{ flexGrow: 1, minWidth: 0 }}>
                <Typography noWrap sx={{ fontWeight: 700 }} variant="body2">
                  {file.name}
                </Typography>
                <Typography color="text.secondary" variant="caption">
                  {formatFileSize(file.size)} • {file.type || 'Belirsiz tür'}
                </Typography>
              </Box>
              <IconButton
                aria-label={`${file.name} dosyasını kaldır`}
                color="inherit"
                onClick={() => onRemoveFile(index)}
                size="small"
              >
                <DeleteOutlined fontSize="small" />
              </IconButton>
            </Stack>
          </Paper>
        )
      }),
    [files, onRemoveFile, previewUrls],
  )

  function openPicker() {
    inputRef.current?.click()
  }

  function handleFilesSelected(fileList: FileList | null) {
    if (!fileList || fileList.length === 0) {
      return
    }

    onAddFiles(Array.from(fileList))

    if (inputRef.current) {
      inputRef.current.value = ''
    }
  }

  function handleDrop(event: DragEvent<HTMLDivElement>) {
    event.preventDefault()
    setDragActive(false)
    handleFilesSelected(event.dataTransfer.files)
  }

  return (
    <Stack spacing={1.4}>
      <Box>
        <Typography sx={{ fontWeight: 700 }} variant="h6">
          Ek Dosyalar
        </Typography>
        <Typography color="text.secondary" sx={{ mt: 0.5 }} variant="body2">
          Hatayı daha iyi anlayabilmemiz için ekran görüntüsü veya ilgili dosyaları
          ekleyebilirsiniz.
        </Typography>
      </Box>

      <Box
        onClick={openPicker}
        onDragEnter={() => setDragActive(true)}
        onDragLeave={() => setDragActive(false)}
        onDragOver={(event) => {
          event.preventDefault()
          setDragActive(true)
        }}
        onDrop={handleDrop}
        role="button"
        sx={{
          background: dragActive
            ? 'linear-gradient(180deg, rgba(245, 240, 255, 0.98), rgba(255,255,255,0.98))'
            : 'linear-gradient(180deg, rgba(245, 240, 255, 0.9), rgba(255,255,255,0.96))',
          border: '1px dashed',
          borderColor: error ? 'error.main' : dragActive ? 'primary.main' : 'rgba(124, 58, 237, 0.26)',
          borderRadius: 4,
          boxShadow: dragActive
            ? '0 18px 38px rgba(124, 58, 237, 0.12)'
            : 'inset 0 1px 0 rgba(255,255,255,0.65)',
          cursor: 'pointer',
          p: { xs: 2.2, sm: 2.5 },
          transition: 'border-color 120ms ease, background-color 120ms ease, box-shadow 120ms ease',
        }}
        tabIndex={0}
      >
        <input
          accept=".png,.jpg,.jpeg,.webp,.pdf"
          hidden
          multiple
          onChange={(event) => handleFilesSelected(event.target.files)}
          ref={inputRef}
          type="file"
        />
        <Stack spacing={1.25} sx={{ alignItems: 'center', textAlign: 'center' }}>
          <Box
            sx={{
              alignItems: 'center',
              bgcolor: 'rgba(124, 58, 237, 0.12)',
              borderRadius: 99,
              color: 'primary.main',
              display: 'grid',
              height: 54,
              placeItems: 'center',
              width: 54,
            }}
          >
            <CloudUploadOutlined />
          </Box>
          <Typography sx={{ fontWeight: 700 }} variant="body1">
            Dosyaları sürükleyip bırakın veya seçmek için tıklayın
          </Typography>
          <Typography color="text.secondary" variant="body2">
            {acceptedTypesLabel} • En fazla 5 MB / dosya • En fazla 5 dosya
          </Typography>
          <Button onClick={openPicker} type="button" variant="outlined">
            Dosya Seç
          </Button>
        </Stack>
      </Box>

      {error ? (
        <Typography color="error.main" variant="caption">
          {error}
        </Typography>
      ) : null}

      {fileCards.length > 0 ? <Stack spacing={1}>{fileCards}</Stack> : null}
    </Stack>
  )
}
