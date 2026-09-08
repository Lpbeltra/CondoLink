import { useRef, useState, type FormEvent } from 'react'
import AttachFileRoundedIcon from '@mui/icons-material/AttachFileRounded'
import CloseRoundedIcon from '@mui/icons-material/CloseRounded'
import SendRoundedIcon from '@mui/icons-material/SendRounded'
import { Alert, Box, Button, Chip, CircularProgress, Dialog, DialogActions, DialogContent, DialogTitle, LinearProgress, Stack, TextField } from '@mui/material'
import { createRequestMessage, uploadRequestAttachments } from '../api'
import { selectAttachmentFiles } from '../attachments'
import { getRequestError } from '../presentation'
import type { RequestMessage } from '../types'

interface Props { requestId: string; onSent: (message: RequestMessage) => Promise<void> | void }

export function ResidentUpdateDialog({ requestId, onSent }: Props) {
  const [open, setOpen] = useState(false)
  const [content, setContent] = useState('')
  const [files, setFiles] = useState<File[]>([])
  const [error, setError] = useState('')
  const [sending, setSending] = useState(false)
  const [progress, setProgress] = useState<number | null>(null)
  const inputRef = useRef<HTMLInputElement>(null)
  const close = () => { if (!sending) { setOpen(false); setError(''); setContent(''); setFiles([]) } }
  const choose = (selected: File[]) => {
    const result = selectAttachmentFiles(files, selected)
    setFiles(result.files); setError(result.error ?? '')
  }
  const submit = async (event: FormEvent) => {
    event.preventDefault()
    if (!content.trim() || sending || content.trim().length > 3000) return
    setSending(true); setError(''); setProgress(null)
    try {
      const message = await createRequestMessage(requestId, content.trim())
      if (files.length) {
        await uploadRequestAttachments(requestId, files, (loaded, total) =>
          setProgress(total ? Math.round(loaded * 100 / total) : null))
      }
      await onSent(message); close()
    } catch (reason) { setError(getRequestError(reason, 'Não foi possível enviar a atualização.')) }
    finally { setSending(false); setProgress(null) }
  }
  return <>
    <Button variant="outlined" onClick={() => setOpen(true)}>Enviar nova atualização</Button>
    <Dialog open={open} onClose={close} fullWidth maxWidth="sm" fullScreen={false}>
      <Box component="form" onSubmit={submit}>
        <DialogTitle>Enviar nova atualização</DialogTitle>
        <DialogContent><Stack spacing={2} mt={1}>
          {error && <Alert severity="error">{error}</Alert>}
          <TextField autoFocus fullWidth multiline minRows={4} label="Nova informação sobre o atendimento"
            value={content} onChange={event => setContent(event.target.value)} disabled={sending}
            inputProps={{ maxLength: 3001 }} error={content.length > 3000} helperText={`${content.length}/3000`} />
          <input ref={inputRef} hidden multiple type="file" accept="image/jpeg,image/png,image/webp,video/mp4,application/pdf,audio/ogg,audio/mpeg,audio/mp4,audio/aac,audio/amr"
            onChange={event => { choose(Array.from(event.target.files ?? [])); event.target.value = '' }} />
          <Stack direction="row" flexWrap="wrap" gap={1}>{files.map((file, index) => <Chip key={`${file.name}-${index}`} label={file.name}
            onDelete={sending ? undefined : () => setFiles(current => current.filter((_, item) => item !== index))} deleteIcon={<CloseRoundedIcon />} />)}</Stack>
          {sending && progress !== null && <LinearProgress variant="determinate" value={progress} />}
          <Button type="button" startIcon={<AttachFileRoundedIcon />} onClick={() => inputRef.current?.click()} disabled={sending || files.length >= 10}>Adicionar anexos</Button>
        </Stack></DialogContent>
        <DialogActions><Button onClick={close} disabled={sending}>Cancelar</Button><Button type="submit" variant="contained"
          disabled={sending || !content.trim() || content.trim().length > 3000}
          startIcon={sending ? <CircularProgress size={18} color="inherit" /> : <SendRoundedIcon />}>{sending ? 'Enviando…' : 'Enviar atualização'}</Button></DialogActions>
      </Box>
    </Dialog>
  </>
}
