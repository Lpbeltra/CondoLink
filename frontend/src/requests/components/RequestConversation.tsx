import { useEffect, useRef, useState, type FormEvent, type KeyboardEvent } from 'react'
import SendRoundedIcon from '@mui/icons-material/SendRounded'
import { Alert, Box, Button, CircularProgress, Stack, TextField, Typography } from '@mui/material'
import { createRequestMessage } from '../api'
import { canSendMessage, formatDateTime, getRequestError } from '../presentation'
import { getUpdateMarkerColor } from '../requestUpdates'
import type { RequestMessage, RequestStatus } from '../types'

interface Props { requestId: string; status: RequestStatus; messages: RequestMessage[]; onMessageCreated: (message: RequestMessage) => void }

export function RequestConversation({ requestId, status, messages, onMessageCreated }: Props) {
  const [content, setContent] = useState('')
  const [error, setError] = useState('')
  const [isSending, setIsSending] = useState(false)
  const endRef = useRef<HTMLDivElement>(null)

  useEffect(() => { endRef.current?.scrollIntoView({ behavior: 'smooth', block: 'nearest' }) }, [messages.length])

  const send = async (event?: FormEvent) => {
    event?.preventDefault()
    const trimmed = content.trim()
    if (!trimmed || trimmed.length > 4000 || isSending) return
    setIsSending(true); setError('')
    try {
      const message = await createRequestMessage(requestId, trimmed)
      onMessageCreated(message); setContent('')
    } catch (requestError) { setError(getRequestError(requestError, 'Não foi possível adicionar a atualização.')) }
    finally { setIsSending(false) }
  }

  const handleKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    if (event.key === 'Enter' && (event.ctrlKey || event.metaKey)) { event.preventDefault(); void send() }
  }

  return (
    <Box>
      <Stack spacing={2} mb={3}>
        {messages.length === 0 && <Typography color="text.secondary">Ainda não há atualizações nesta solicitação.</Typography>}
        {messages.map((message) => (
          <Box key={message.id} borderLeft="4px solid" borderColor={getUpdateMarkerColor(message)} pl={2} py={.5}>
            <Typography fontWeight={750} fontSize=".8rem">{message.author.fullName}</Typography>
            <Typography sx={{ whiteSpace: 'pre-wrap', overflowWrap: 'anywhere' }}>{message.content}</Typography>
            <Typography color="text.secondary" fontSize=".72rem" mt={.75}>{formatDateTime(message.createdAt)}</Typography>
          </Box>
        ))}
        <div ref={endRef} />
      </Stack>
      {!canSendMessage(status) ? <Alert severity="info">Esta solicitação foi cancelada e não aceita novas atualizações.</Alert> : (
        <Box component="form" onSubmit={send}>
          {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
          <TextField fullWidth multiline minRows={3} maxRows={8} label="Descreva uma nova informação sobre a solicitação" value={content} onChange={(event) => setContent(event.target.value)} onKeyDown={handleKeyDown} inputProps={{ maxLength: 4000 }} helperText={`${content.length}/4000 · Ctrl + Enter para adicionar`} disabled={isSending} />
          <Box display="flex" justifyContent="flex-end" mt={1.5}><Button type="submit" variant="contained" disabled={!content.trim() || isSending} startIcon={isSending ? <CircularProgress size={18} color="inherit" /> : <SendRoundedIcon />}>{isSending ? 'Adicionando…' : 'Adicionar atualização'}</Button></Box>
        </Box>
      )}
    </Box>
  )
}
