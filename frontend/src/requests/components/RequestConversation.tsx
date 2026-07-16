import { useEffect, useRef, useState, type FormEvent, type KeyboardEvent } from 'react'
import SendRoundedIcon from '@mui/icons-material/SendRounded'
import { Alert, Box, Button, CircularProgress, Stack, TextField, Typography } from '@mui/material'
import { useAuth } from '../../auth/AuthContext'
import { createRequestMessage } from '../api'
import { canSendMessage, formatDateTime, getRequestError } from '../presentation'
import type { RequestMessage, RequestStatus } from '../types'

interface Props { requestId: string; status: RequestStatus; messages: RequestMessage[]; onMessageCreated: (message: RequestMessage) => void }

export function RequestConversation({ requestId, status, messages, onMessageCreated }: Props) {
  const { user } = useAuth()
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
    } catch (requestError) { setError(getRequestError(requestError, 'Não foi possível enviar sua mensagem.')) }
    finally { setIsSending(false) }
  }

  const handleKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    if (event.key === 'Enter' && (event.ctrlKey || event.metaKey)) { event.preventDefault(); void send() }
  }

  return (
    <Box>
      <Stack spacing={2} mb={3}>
        {messages.length === 0 && <Typography color="text.secondary">Ainda não há mensagens nesta conversa.</Typography>}
        {messages.map((message) => {
          const mine = message.author.id === user?.id
          return <Box key={message.id} alignSelf={mine ? 'flex-end' : 'flex-start'} maxWidth={{ xs: '88%', sm: '72%' }} bgcolor={mine ? 'rgba(31,94,255,.09)' : 'background.default'} border="1px solid" borderColor={mine ? 'rgba(31,94,255,.16)' : 'divider'} borderRadius={3} px={2} py={1.5}>
            <Typography fontWeight={750} fontSize=".8rem">{mine ? 'Você' : message.author.fullName}</Typography>
            <Typography sx={{ whiteSpace: 'pre-wrap', overflowWrap: 'anywhere' }}>{message.content}</Typography>
            <Typography color="text.secondary" fontSize=".72rem" mt={.75}>{formatDateTime(message.createdAt)}</Typography>
          </Box>
        })}
        <div ref={endRef} />
      </Stack>
      {!canSendMessage(status) ? <Alert severity="info">Esta solicitação foi cancelada e não aceita novas mensagens.</Alert> : (
        <Box component="form" onSubmit={send}>
          {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
          <TextField fullWidth multiline minRows={3} maxRows={8} label="Escreva uma mensagem" value={content} onChange={(event) => setContent(event.target.value)} onKeyDown={handleKeyDown} inputProps={{ maxLength: 4000 }} helperText={`${content.length}/4000 · Ctrl + Enter para enviar`} disabled={isSending} />
          <Box display="flex" justifyContent="flex-end" mt={1.5}><Button type="submit" variant="contained" disabled={!content.trim() || isSending} startIcon={isSending ? <CircularProgress size={18} color="inherit" /> : <SendRoundedIcon />}>{isSending ? 'Enviando…' : 'Enviar mensagem'}</Button></Box>
        </Box>
      )}
    </Box>
  )
}
