import { useCallback, useEffect, useRef, useState } from 'react'
import AttachFileRoundedIcon from '@mui/icons-material/AttachFileRounded'
import DescriptionRoundedIcon from '@mui/icons-material/DescriptionRounded'
import OpenInNewRoundedIcon from '@mui/icons-material/OpenInNewRounded'
import { Alert, Box, Button, Card, CardContent, CircularProgress, Dialog, DialogContent, DialogTitle, Stack, Typography } from '@mui/material'
import { getErrorMessage } from '../../services/api'
import { getRequestAttachmentBlob, listRequestAttachments, uploadRequestAttachments } from '../api'
import { formatDateTime } from '../presentation'
import type { RequestAttachment } from '../types'

const allowedTypes = ['image/jpeg', 'image/png', 'image/webp', 'application/pdf']
const maximumSize = 10 * 1024 * 1024

function formatSize(bytes: number) {
  return bytes < 1024 * 1024 ? `${Math.ceil(bytes / 1024)} KB` : `${(bytes / 1024 / 1024).toFixed(1)} MB`
}

export function RequestAttachments({ requestId, cancelled }: { requestId: string; cancelled: boolean }) {
  const inputRef = useRef<HTMLInputElement>(null)
  const [items, setItems] = useState<RequestAttachment[]>([])
  const [previews, setPreviews] = useState<Record<string, string>>({})
  const [selected, setSelected] = useState<File[]>([])
  const [dialogUrl, setDialogUrl] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [uploading, setUploading] = useState(false)
  const [error, setError] = useState('')

  const load = useCallback(async () => {
    setLoading(true); setError('')
    try { setItems(await listRequestAttachments(requestId)) }
    catch (requestError) { setError(getErrorMessage(requestError)) }
    finally { setLoading(false) }
  }, [requestId])

  useEffect(() => { void load() }, [load])
  useEffect(() => {
    let active = true
    const urls: string[] = []
    void Promise.all(items.filter((item) => item.contentType.startsWith('image/')).map(async (item) => {
      const blob = await getRequestAttachmentBlob(item.contentUrl)
      const url = URL.createObjectURL(blob); urls.push(url)
      if (active) setPreviews((current) => ({ ...current, [item.id]: url }))
    })).catch(() => { if (active) setError('Não foi possível carregar uma das miniaturas.') })
    return () => { active = false; urls.forEach(URL.revokeObjectURL); setPreviews({}) }
  }, [items])

  const chooseFiles = (files: File[]) => {
    setError('')
    if (files.length > 5) return setError('Selecione no máximo cinco arquivos.')
    if (files.some((file) => !allowedTypes.includes(file.type))) return setError('Envie somente JPG, PNG, WebP ou PDF.')
    if (files.some((file) => file.size > maximumSize)) return setError('Cada arquivo deve ter no máximo 10 MB.')
    setSelected(files)
  }

  const upload = async () => {
    if (!selected.length || uploading) return
    setUploading(true); setError('')
    try { await uploadRequestAttachments(requestId, selected); setSelected([]); if (inputRef.current) inputRef.current.value = ''; await load() }
    catch (requestError) { setError(getErrorMessage(requestError)) }
    finally { setUploading(false) }
  }

  const openPdf = async (item: RequestAttachment) => {
    try {
      const url = URL.createObjectURL(await getRequestAttachmentBlob(item.contentUrl))
      window.open(url, '_blank', 'noopener,noreferrer')
      window.setTimeout(() => URL.revokeObjectURL(url), 60_000)
    } catch (requestError) { setError(getErrorMessage(requestError)) }
  }

  return <Card elevation={0} sx={{ mt: 3 }}><CardContent sx={{ p: { xs: 2.5, sm: 4 } }}>
    <Stack direction={{ xs: 'column', sm: 'row' }} justifyContent="space-between" gap={2} mb={2}>
      <Box><Typography variant="h2">Anexos</Typography><Typography color="text.secondary" fontSize=".875rem">Imagens e PDFs de até 10 MB.</Typography></Box>
      {!cancelled && <Button component="label" variant="outlined" startIcon={<AttachFileRoundedIcon />} disabled={uploading} sx={{ minHeight: 44 }}>
        Adicionar anexo<input ref={inputRef} hidden type="file" multiple accept="image/jpeg,image/png,image/webp,application/pdf" onChange={(event) => chooseFiles(Array.from(event.target.files ?? []))} />
      </Button>}
    </Stack>
    {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
    {!!selected.length && <Stack gap={1} mb={2}><Typography fontWeight={700}>{selected.map((file) => file.name).join(', ')}</Typography><Button variant="contained" onClick={() => void upload()} disabled={uploading}>{uploading ? <CircularProgress size={22} color="inherit" /> : `Enviar ${selected.length} arquivo(s)`}</Button></Stack>}
    {loading ? <CircularProgress size={24} /> : items.length === 0 ? <Typography color="text.secondary">Nenhum anexo enviado.</Typography> :
      <Box display="grid" gridTemplateColumns={{ xs: '1fr', sm: 'repeat(2, minmax(0, 1fr))' }} gap={1.5}>
        {items.map((item) => <Box key={item.id} sx={{ border: '1px solid', borderColor: 'divider', borderRadius: 2, p: 1.5, minWidth: 0 }}>
          <Stack direction="row" gap={1.5} alignItems="center">
            {item.contentType.startsWith('image/') && previews[item.id] ? <Box component="img" src={previews[item.id]} alt="" sx={{ width: 64, height: 64, objectFit: 'cover', borderRadius: 1, cursor: 'pointer' }} onClick={() => setDialogUrl(previews[item.id])} /> : <DescriptionRoundedIcon color="action" sx={{ fontSize: 42 }} />}
            <Box minWidth={0} flex={1}><Typography fontWeight={700} noWrap title={item.originalFileName}>{item.originalFileName}</Typography><Typography color="text.secondary" fontSize=".78rem">{formatSize(item.fileSize)} · {item.uploadedBy.fullName}</Typography><Typography color="text.secondary" fontSize=".75rem">{formatDateTime(item.createdAt)}</Typography></Box>
            <Button aria-label={`Abrir ${item.originalFileName}`} onClick={() => item.contentType.startsWith('image/') ? setDialogUrl(previews[item.id]) : void openPdf(item)}><OpenInNewRoundedIcon /></Button>
          </Stack>
        </Box>)}
      </Box>}
    <Dialog open={Boolean(dialogUrl)} onClose={() => setDialogUrl(null)} maxWidth="lg" fullWidth><DialogTitle>Visualização do anexo</DialogTitle><DialogContent sx={{ textAlign: 'center', overflow: 'auto' }}>{dialogUrl && <Box component="img" src={dialogUrl} alt="Anexo ampliado" sx={{ maxWidth: '100%', maxHeight: '75vh', objectFit: 'contain' }} />}</DialogContent></Dialog>
  </CardContent></Card>
}
