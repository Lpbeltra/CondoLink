import { useCallback, useEffect, useRef, useState } from 'react'
import AttachFileRoundedIcon from '@mui/icons-material/AttachFileRounded'
import CloseRoundedIcon from '@mui/icons-material/CloseRounded'
import DeleteOutlineRoundedIcon from '@mui/icons-material/DeleteOutlineRounded'
import DescriptionRoundedIcon from '@mui/icons-material/DescriptionRounded'
import DownloadRoundedIcon from '@mui/icons-material/DownloadRounded'
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  IconButton,
  LinearProgress,
  Stack,
  Typography,
} from '@mui/material'
import {
  deleteRequestAttachment,
  getRequestAttachmentBlob,
  listRequestAttachments,
  uploadRequestAttachments,
} from '../api'
import {
  appendUploadedAttachments,
  calculateUploadProgress,
  formatAttachmentSize,
  getAttachmentErrorMessage,
  removeSelectedAttachment,
  removeUploadedAttachment,
  selectAttachmentFiles,
} from '../attachments'
import { formatDateTime } from '../presentation'
import type { RequestAttachment } from '../types'

interface RequestAttachmentsProps {
  requestId: string
  readOnly?: boolean
}

export function RequestAttachments({
  requestId,
  readOnly = false,
}: RequestAttachmentsProps) {
  const inputRef = useRef<HTMLInputElement>(null)
  const [items, setItems] = useState<RequestAttachment[]>([])
  const [previews, setPreviews] = useState<Record<string, string>>({})
  const [selected, setSelected] = useState<File[]>([])
  const [dialogUrl, setDialogUrl] = useState<string | null>(null)
  const [dialogItem, setDialogItem] = useState<RequestAttachment | null>(null)
  const [previewLoading, setPreviewLoading] = useState(false)
  const [deleteTarget, setDeleteTarget] = useState<RequestAttachment | null>(null)
  const [loading, setLoading] = useState(true)
  const [uploading, setUploading] = useState(false)
  const [deleting, setDeleting] = useState(false)
  const [uploadProgress, setUploadProgress] = useState(0)
  const [error, setError] = useState('')
  const imageItems = items.filter(item =>
    item.contentType.toLowerCase().startsWith('image/'))
  const otherItems = items.filter(item =>
    !item.contentType.toLowerCase().startsWith('image/'))

  const load = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      setItems(await listRequestAttachments(requestId))
    } catch (requestError) {
      setError(getAttachmentErrorMessage(requestError))
    } finally {
      setLoading(false)
    }
  }, [requestId])

  useEffect(() => {
    void load()
  }, [load])

  useEffect(() => {
    let active = true
    const urls: string[] = []
    void Promise.all(
      items
        .filter(item => item.contentType.startsWith('image/'))
        .map(async item => {
          const blob = await getRequestAttachmentBlob(item.contentUrl)
          const url = URL.createObjectURL(blob)
          if (!active) {
            URL.revokeObjectURL(url)
            return
          }
          urls.push(url)
          setPreviews(current => ({ ...current, [item.id]: url }))
        }),
    ).catch(() => {
      if (active) setError('Não foi possível carregar uma das miniaturas.')
    })

    return () => {
      active = false
      if (typeof URL.revokeObjectURL === 'function') {
        urls.forEach(url => URL.revokeObjectURL(url))
      }
      setPreviews({})
    }
  }, [items])

  const chooseFiles = (files: File[]) => {
    const result = selectAttachmentFiles(selected, files)
    setError(result.error ?? '')
    setSelected(result.files)
    if (inputRef.current) inputRef.current.value = ''
  }

  const upload = async () => {
    if (!selected.length || uploading) return

    setUploading(true)
    setUploadProgress(0)
    setError('')
    try {
      const uploaded = await uploadRequestAttachments(
        requestId,
        selected,
        (loaded, total) =>
          setUploadProgress(calculateUploadProgress(loaded, total)),
      )
      setUploadProgress(100)
      setItems(current => appendUploadedAttachments(current, uploaded))
      setSelected([])
      if (inputRef.current) inputRef.current.value = ''
    } catch (requestError) {
      setError(getAttachmentErrorMessage(requestError))
    } finally {
      setUploading(false)
    }
  }

  const download = async (item: RequestAttachment) => {
    try {
      const url = URL.createObjectURL(
        await getRequestAttachmentBlob(item.contentUrl),
      )
      const anchor = document.createElement('a')
      anchor.href = url
      anchor.download = item.originalFileName
      document.body.appendChild(anchor)
      anchor.click()
      anchor.remove()
      URL.revokeObjectURL(url)
    } catch (requestError) {
      setError(getAttachmentErrorMessage(requestError))
    }
  }

  const openPreview = async (item: RequestAttachment) => {
    setPreviewLoading(true)
    setError('')
    try {
      const url = URL.createObjectURL(await getRequestAttachmentBlob(item.contentUrl))
      setDialogItem(item)
      setDialogUrl(url)
    } catch (requestError) {
      setError(getAttachmentErrorMessage(requestError))
    } finally {
      setPreviewLoading(false)
    }
  }

  const closePreview = () => {
    setDialogUrl(null)
    setDialogItem(null)
  }

  useEffect(() => {
    if (!dialogUrl || !dialogItem
      || dialogItem.contentType.startsWith('image/')) return
    return () => {
      if (typeof URL.revokeObjectURL === 'function')
        URL.revokeObjectURL(dialogUrl)
    }
  }, [dialogUrl, dialogItem])

  const confirmDelete = async () => {
    if (!deleteTarget || deleting) return

    setDeleting(true)
    setError('')
    try {
      await deleteRequestAttachment(deleteTarget.id)
      setItems(current =>
        removeUploadedAttachment(current, deleteTarget.id),
      )
      setDeleteTarget(null)
    } catch (requestError) {
      setError(getAttachmentErrorMessage(requestError))
    } finally {
      setDeleting(false)
    }
  }

  return (
    <Card elevation={0} sx={{ mt: 3 }}>
      <CardContent sx={{ p: { xs: 2.5, sm: 4 } }}>
        <Stack
          direction={{ xs: 'column', sm: 'row' }}
          justifyContent="space-between"
          gap={2}
          mb={2}
        >
          <Box>
            <Typography variant="h2">Outros anexos</Typography>
            <Typography color="text.secondary" fontSize=".875rem">
              Imagens e PDFs. Até 6 arquivos por envio e 15 MB por arquivo.
            </Typography>
          </Box>
          {!readOnly && (
            <Button
              component="label"
              variant="outlined"
              startIcon={<AttachFileRoundedIcon />}
              disabled={uploading}
              sx={{ minHeight: 44 }}
            >
              Adicionar anexos
              <input
                ref={inputRef}
                hidden
                type="file"
                multiple
                accept=".jpg,.jpeg,.png,.webp,.pdf,image/jpeg,image/png,image/webp,application/pdf"
                onChange={event =>
                  chooseFiles(Array.from(event.target.files ?? []))}
              />
            </Button>
          )}
        </Stack>

        {error && (
          <Alert severity="error" sx={{ mb: 2 }}>
            {error}
          </Alert>
        )}

        {!!selected.length && (
          <Box
            sx={{
              border: '1px solid',
              borderColor: 'divider',
              borderRadius: 2,
              p: 2,
              mb: 2,
            }}
          >
            <Typography fontWeight={700} mb={1}>
              {selected.length}{' '}
              {selected.length === 1
                ? 'arquivo selecionado'
                : 'arquivos selecionados'}
            </Typography>
            <Stack gap={0.5} mb={2}>
              {selected.map((file, index) => (
                <Stack
                  key={`${file.name}-${file.size}-${index}`}
                  direction="row"
                  alignItems="center"
                  gap={1}
                >
                  <Box minWidth={0} flex={1}>
                    <Typography noWrap title={file.name}>
                      {file.name}
                    </Typography>
                    <Typography color="text.secondary" fontSize=".78rem">
                      {formatAttachmentSize(file.size)}
                    </Typography>
                  </Box>
                  <IconButton
                    aria-label={`Remover ${file.name}`}
                    disabled={uploading}
                    onClick={() =>
                      setSelected(current =>
                        removeSelectedAttachment(current, index))}
                  >
                    <CloseRoundedIcon />
                  </IconButton>
                </Stack>
              ))}
            </Stack>

            {uploading && (
              <Box mb={2}>
                <Stack direction="row" justifyContent="space-between" mb={0.5}>
                  <Typography fontSize=".85rem">Enviando arquivos</Typography>
                  <Typography fontSize=".85rem" fontWeight={700}>
                    {uploadProgress}%
                  </Typography>
                </Stack>
                <LinearProgress
                  variant="determinate"
                  value={uploadProgress}
                  aria-label={`Progresso do upload: ${uploadProgress}%`}
                />
              </Box>
            )}

            <Stack direction={{ xs: 'column', sm: 'row' }} gap={1}>
              <Button
                variant="contained"
                onClick={() => void upload()}
                disabled={!selected.length || uploading}
              >
                {uploading ? 'Enviando…' : 'Enviar arquivos'}
              </Button>
              {!uploading && (
                <Button variant="text" onClick={() => setSelected([])}>
                  Cancelar seleção
                </Button>
              )}
            </Stack>
          </Box>
        )}

        {!loading && imageItems.length > 0 && <Box mb={2.5}>
          <Typography variant="h3" mb={1.5}>Galeria de imagens</Typography>
          <Box display="grid" gridTemplateColumns={{ xs: 'repeat(2, minmax(0, 1fr))', sm: 'repeat(3, minmax(0, 1fr))', md: 'repeat(4, minmax(0, 1fr))' }} gap={1.5}>
            {imageItems.map(item => <Box key={item.id} sx={{ border: '1px solid', borderColor: 'divider', borderRadius: 2, overflow: 'hidden', minWidth: 0 }}>
              <Box component="button" type="button" aria-label={`Ampliar ${item.originalFileName}`} onClick={() => { if (previews[item.id]) { setDialogItem(item); setDialogUrl(previews[item.id]) } }} sx={{ p: 0, border: 0, background: 'action.hover', cursor: previews[item.id] ? 'pointer' : 'default', width: '100%', height: { xs: 132, sm: 168 }, display: 'block' }}>
                {previews[item.id] && <Box component="img" src={previews[item.id]} alt={`Miniatura de ${item.originalFileName}`} sx={{ width: '100%', height: '100%', objectFit: 'cover', display: 'block' }} />}
              </Box>
              <Box p={1.25}>
                <Typography fontWeight={700} noWrap title={item.originalFileName}>{item.originalFileName}</Typography>
                <Typography color="text.secondary" fontSize=".75rem">{formatDateTime(item.createdAt)}</Typography>
                <Stack direction="row" justifyContent="flex-end">
                  <IconButton aria-label={`Baixar ${item.originalFileName}`} onClick={() => void download(item)}><DownloadRoundedIcon /></IconButton>
                  {!readOnly && <IconButton aria-label={`Excluir ${item.originalFileName}`} onClick={() => setDeleteTarget(item)}><DeleteOutlineRoundedIcon /></IconButton>}
                </Stack>
              </Box>
            </Box>)}
          </Box>
        </Box>}

        {loading ? (
          <CircularProgress size={24} />
        ) : otherItems.length === 0 && imageItems.length === 0 ? (
          <Typography color="text.secondary">
            Nenhum anexo enviado.
          </Typography>
        ) : otherItems.length > 0 ? (
          <Box
            display="grid"
            gridTemplateColumns={{
              xs: '1fr',
              sm: 'repeat(2, minmax(0, 1fr))',
            }}
            gap={1.5}
          >
            {otherItems.map(item => (
              <Box
                key={item.id}
                sx={{
                  border: '1px solid',
                  borderColor: 'divider',
                  borderRadius: 2,
                  p: 1.5,
                  minWidth: 0,
                }}
              >
                <Stack direction="row" gap={1.5} alignItems="center">
                  {item.contentType.startsWith('image/')
                    && previews[item.id] ? (
                      <Box
                        component="button"
                        type="button"
                        aria-label={`Ampliar ${item.originalFileName}`}
                        onClick={() => { setDialogItem(item); setDialogUrl(previews[item.id]) }}
                        sx={{
                          p: 0,
                          border: 0,
                          background: 'none',
                          cursor: 'pointer',
                          width: 64,
                          height: 64,
                          borderRadius: 1,
                          flexShrink: 0,
                          '&:focus-visible': {
                            outline: '3px solid',
                            outlineColor: 'primary.light',
                            outlineOffset: 2,
                          },
                        }}
                      >
                        <Box
                          component="img"
                          src={previews[item.id]}
                          alt={`Miniatura de ${item.originalFileName}`}
                          sx={{
                            width: '100%',
                            height: '100%',
                            objectFit: 'cover',
                            borderRadius: 1,
                            display: 'block',
                          }}
                        />
                      </Box>
                    ) : (
                      <DescriptionRoundedIcon
                        color="action"
                        sx={{ fontSize: 42 }}
                      />
                    )}
                  <Box minWidth={0} flex={1}>
                    <Typography
                      fontWeight={700}
                      noWrap
                      title={item.originalFileName}
                    >
                      {item.originalFileName}
                    </Typography>
                    <Typography color="text.secondary" fontSize=".78rem">
                      {formatAttachmentSize(item.fileSize)}
                      {' · '}
                      {item.uploadedBy.fullName}
                    </Typography>
                    <Typography color="text.secondary" fontSize=".75rem">
                      {formatDateTime(item.createdAt)}
                    </Typography>
                  </Box>
                  <Stack direction="row">
                    {(item.contentType.startsWith('audio/')
                      || item.contentType.startsWith('video/')
                      || item.contentType === 'application/pdf') && (
                      <Button size="small" disabled={previewLoading}
                        onClick={() => void openPreview(item)}>Visualizar</Button>
                    )}
                    <IconButton
                      aria-label={`Baixar ${item.originalFileName}`}
                      onClick={() => void download(item)}
                    >
                      <DownloadRoundedIcon />
                    </IconButton>
                    {!readOnly && (
                      <IconButton
                        aria-label={`Excluir ${item.originalFileName}`}
                        onClick={() => setDeleteTarget(item)}
                      >
                        <DeleteOutlineRoundedIcon />
                      </IconButton>
                    )}
                  </Stack>
                </Stack>
              </Box>
            ))}
          </Box>
        ) : null}
      </CardContent>

      <Dialog
        open={Boolean(dialogUrl)}
        onClose={closePreview}
        maxWidth="lg"
        fullWidth
      >
        <DialogTitle>Visualização do anexo</DialogTitle>
        <DialogContent sx={{ textAlign: 'center', overflow: 'auto' }}>
          {dialogUrl && dialogItem?.contentType.startsWith('image/') && (
            <Box
              component="img"
              src={dialogUrl}
              alt="Anexo ampliado"
              sx={{
                maxWidth: '100%',
                maxHeight: '75vh',
                objectFit: 'contain',
              }}
            />
          )}
          {dialogUrl && dialogItem?.contentType.startsWith('audio/') && (
            <Box component="audio" src={dialogUrl} controls autoPlay={false}
              sx={{ width: '100%' }} />
          )}
          {dialogUrl && dialogItem?.contentType.startsWith('video/') && (
            <Box component="video" src={dialogUrl} controls autoPlay={false}
              sx={{ width: '100%', maxHeight: '75vh' }} />
          )}
          {dialogUrl && dialogItem?.contentType === 'application/pdf' && (
            <Box component="iframe" src={dialogUrl}
              title={`Visualização de ${dialogItem.originalFileName}`}
              sx={{ border: 0, width: '100%', height: '75vh' }} />
          )}
        </DialogContent>
        <DialogActions><Button onClick={closePreview}>Fechar</Button></DialogActions>
      </Dialog>

      <Dialog
        open={Boolean(deleteTarget)}
        onClose={() => {
          if (!deleting) setDeleteTarget(null)
        }}
      >
        <DialogTitle>Excluir anexo?</DialogTitle>
        <DialogContent>
          <Typography>
            O arquivo “{deleteTarget?.originalFileName}” será excluído.
          </Typography>
        </DialogContent>
        <DialogActions>
          <Button
            onClick={() => setDeleteTarget(null)}
            disabled={deleting}
          >
            Cancelar
          </Button>
          <Button
            color="error"
            variant="contained"
            onClick={() => void confirmDelete()}
            disabled={deleting}
          >
            {deleting ? 'Excluindo…' : 'Excluir'}
          </Button>
        </DialogActions>
      </Dialog>
    </Card>
  )
}
