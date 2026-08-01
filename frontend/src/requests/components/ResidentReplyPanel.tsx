import { useRef, useState, type FormEvent } from 'react'
import AttachFileRoundedIcon from '@mui/icons-material/AttachFileRounded'
import CloseRoundedIcon from '@mui/icons-material/CloseRounded'
import SendRoundedIcon from '@mui/icons-material/SendRounded'
import { Alert, Box, Button, Card, CardContent, Chip, CircularProgress, LinearProgress, Stack, TextField, Typography } from '@mui/material'
import { createResidentReply } from '../api'
import { formatDateTime, getRequestError } from '../presentation'
import type { ResidentReplyRequirement } from '../types'

interface Props { requestId: string; requirement: ResidentReplyRequirement; onSent: () => Promise<void> | void }

export function ResidentReplyPanel({ requestId, requirement, onSent }: Props) {
  const [message, setMessage] = useState(''); const [files, setFiles] = useState<File[]>([])
  const [error, setError] = useState(''); const [sending, setSending] = useState(false)
  const [progress, setProgress] = useState<number | null>(null); const inputRef = useRef<HTMLInputElement>(null)
  const submit = (event: FormEvent) => {
    event.preventDefault()
    if ((!message.trim() && files.length === 0) || sending) { setError('Informe uma resposta ou selecione ao menos um arquivo.'); return }
    setSending(true); setError(''); setProgress(0)
    Promise.resolve(createResidentReply(requestId, message, files,
      (loaded, total) => setProgress(total ? Math.round(loaded * 100 / total) : null)))
      .then(() => onSent())
      .catch(requestError => setError(getRequestError(requestError, 'Não foi possível enviar sua resposta.')))
      .finally(() => { setSending(false); setProgress(null) })
  }
  return <Card elevation={0} sx={{ mt: 3, borderColor: 'warning.main', borderWidth: 2 }}><CardContent sx={{ p: { xs: 2.5, sm: 4 } }}>
    <Typography variant="h2">A administração precisa de uma informação sua</Typography>
    <Typography color="text.secondary" fontSize=".8rem" mt={.5}>{formatDateTime(requirement.requestedAt)}</Typography>
    <Alert severity="warning" sx={{ my: 2, whiteSpace: 'pre-wrap' }}>{requirement.question}</Alert>
    <Box component="form" onSubmit={submit}>{error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
      <TextField fullWidth multiline minRows={3} maxRows={8} label="Sua resposta" value={message} onChange={event => setMessage(event.target.value)} disabled={sending} inputProps={{ maxLength: 4000 }} />
      <input ref={inputRef} hidden multiple type="file" accept="image/jpeg,image/png,image/webp,video/mp4,application/pdf,audio/ogg,audio/mpeg,audio/mp4,audio/aac,audio/amr" onChange={event => { setFiles(current => [...current, ...Array.from(event.target.files ?? [])].slice(0, 10)); event.target.value = '' }} />
      <Stack direction="row" flexWrap="wrap" gap={1} mt={2}>{files.map((file, index) => <Chip key={`${file.name}-${index}`} label={file.name} onDelete={sending ? undefined : () => setFiles(current => current.filter((_, itemIndex) => itemIndex !== index))} deleteIcon={<CloseRoundedIcon />} />)}</Stack>
      {sending && progress !== null && <LinearProgress variant="determinate" value={progress} sx={{ mt: 2 }} />}
      <Stack direction={{ xs: 'column', sm: 'row' }} justifyContent="space-between" gap={1.5} mt={2}><Button type="button" startIcon={<AttachFileRoundedIcon />} onClick={() => inputRef.current?.click()} disabled={sending || files.length >= 10}>Adicionar anexos</Button><Button type="submit" variant="contained" disabled={sending || (!message.trim() && files.length === 0)} startIcon={sending ? <CircularProgress size={18} color="inherit" /> : <SendRoundedIcon />}>{sending ? 'Enviando…' : 'Enviar resposta'}</Button></Stack>
    </Box>
  </CardContent></Card>
}
