import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react'
import { Alert, Box, Button, Card, CardContent, CircularProgress, Stack, TextField, Typography } from '@mui/material'
import { createInternalNote, deleteInternalNote, editInternalNote, listInternalNotes, type RequestInternalNote } from '../internalNotes'
import { formatDateTime, getRequestError } from '../presentation'
import { useVisiblePolling } from '../../hooks/useVisiblePolling'

export function RequestInternalNotes({ requestId, onChanged }: { requestId: string; onChanged?: () => Promise<void> | void }) {
  const [notes, setNotes] = useState<RequestInternalNote[]>([])
  const [content, setContent] = useState('')
  const [editing, setEditing] = useState<string | null>(null)
  const [editContent, setEditContent] = useState('')
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const version = useRef(0)
  const load = useCallback(async () => {
    const current = ++version.current
    try {
      const rows = await listInternalNotes(requestId)
      if (current === version.current) { setNotes(rows); setError('') }
    } catch (reason) {
      if (current === version.current) { setNotes([]); setError(getRequestError(reason, 'Não foi possível carregar as notas internas.')) }
    } finally { if (current === version.current) setLoading(false) }
  }, [requestId])
  useEffect(() => { void load() }, [load])
  const poll = useCallback(async () => { if (!busy) await load() }, [busy, load])
  useVisiblePolling(poll)

  const mutate = async (operation: () => Promise<void>) => {
    if (busy) return
    ++version.current
    setBusy(true); setError('')
    try { await operation() }
    catch (reason) { setError(getRequestError(reason, 'Não foi possível salvar a alteração na nota.')) }
    finally { setBusy(false) }
  }
  const create = (event: FormEvent) => {
    event.preventDefault()
    if (!content.trim() || content.trim().length > 3000) return
    void mutate(async () => {
      const note = await createInternalNote(requestId, content.trim())
      setNotes(current => [note, ...current]); setContent(''); await onChanged?.()
    })
  }
  const save = (noteId: string) => {
    if (!editContent.trim() || editContent.trim().length > 3000) return
    void mutate(async () => {
      const note = await editInternalNote(requestId, noteId, editContent.trim())
      setNotes(current => current.map(item => item.id === noteId ? note : item)); setEditing(null); await onChanged?.()
    })
  }

  return <Card elevation={0} sx={{ mt: 3 }}><CardContent sx={{ p: { xs: 2.5, sm: 4 } }}>
    <Typography variant="h2" mb={.5}>Notas internas</Typography>
    <Typography color="text.secondary" mb={2}>Visíveis apenas para a gestão autorizada deste atendimento.</Typography>
    {error && <Alert severity="error" sx={{ mb: 2 }} action={<Button color="inherit" disabled={busy} onClick={() => void load()}>Atualizar</Button>}>{error}</Alert>}
    {loading ? <CircularProgress size={24} aria-label="Carregando notas internas" /> : <>
      <Box component="form" onSubmit={create} mb={3}>
        <TextField label="Nova nota interna" value={content} onChange={event => setContent(event.target.value)}
          fullWidth multiline minRows={3} disabled={busy} inputProps={{ maxLength: 3001 }}
          error={content.length > 3000} helperText={`${content.length}/3000`} />
        <Button type="submit" variant="outlined" sx={{ mt: 1 }} disabled={busy || !content.trim() || content.trim().length > 3000}>Adicionar nota</Button>
      </Box>
      {!notes.length && <Typography color="text.secondary">Nenhuma nota interna.</Typography>}
      <Stack spacing={2}>{notes.map(note => <Box component="article" key={note.id} aria-label={`Nota de ${note.author.fullName}`}
        sx={{ border: '1px solid', borderColor: 'divider', borderRadius: 2, p: 2, minWidth: 0 }}>
        <Typography color="text.secondary" fontSize=".8rem" mb={1}>{note.author.fullName} · {formatDateTime(note.createdAt)}{note.updatedAt && ` · Editada em ${formatDateTime(note.updatedAt)}`}</Typography>
        {editing === note.id ? <>
          <TextField label="Editar nota interna" value={editContent} onChange={event => setEditContent(event.target.value)}
            fullWidth multiline minRows={3} disabled={busy} inputProps={{ maxLength: 3001 }}
            error={editContent.length > 3000} helperText={`${editContent.length}/3000`} />
          <Stack direction="row" gap={1} mt={1}>
            <Button disabled={busy || !editContent.trim() || editContent.trim().length > 3000} onClick={() => save(note.id)}>Salvar nota</Button>
            <Button disabled={busy} onClick={() => setEditing(null)}>Cancelar edição</Button>
          </Stack>
        </> : <>
          <Typography sx={{ whiteSpace: 'pre-wrap', overflowWrap: 'anywhere', lineHeight: 1.7 }}>{note.content}</Typography>
          <Stack direction="row" gap={1} mt={1}>
            <Button size="small" disabled={busy} onClick={() => { setEditing(note.id); setEditContent(note.content) }}>Editar</Button>
            <Button size="small" color="error" disabled={busy} onClick={() => void mutate(async () => {
              await deleteInternalNote(requestId, note.id); setNotes(current => current.filter(item => item.id !== note.id)); await onChanged?.()
            })}>Excluir</Button>
          </Stack>
        </>}
      </Box>)}</Stack>
    </>}
  </CardContent></Card>
}
