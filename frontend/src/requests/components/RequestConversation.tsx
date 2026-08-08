import { useState, type FormEvent, type KeyboardEvent } from 'react'
import SendRoundedIcon from '@mui/icons-material/SendRounded'
import { Alert, Box, Button, CircularProgress, Stack, TextField, Typography } from '@mui/material'
import { createRequestMessage } from '../api'
import { canSendMessage, formatDateTime, getRequestError } from '../presentation'
import { getUpdateMarkerColor } from '../requestUpdates'
import type { RequestMessage, RequestStatus } from '../types'

interface Props { requestId: string; status: RequestStatus; messages: RequestMessage[]; onMessageCreated: (message: RequestMessage) => void; readOnly?: boolean; residentSummary?: string | null }

export function RequestConversation({ requestId, status, messages, onMessageCreated, readOnly = false, residentSummary }: Props) {
  const [content, setContent] = useState('')
  const [error, setError] = useState('')
  const [isSending, setIsSending] = useState(false)
  const orderedMessages = [...messages]
    .filter(message => message.author.isManager
      || message.channel === 'WhatsAppResidentUpdate'
      || message.isResidentReply)
    .sort((left, right) =>
    right.createdAt.localeCompare(left.createdAt) || right.id.localeCompare(left.id))
  const latestResidentId = orderedMessages.find(message => !message.author.isManager)?.id
  const seen = new Set<string>()
  const timelineMessages = orderedMessages.filter(message => {
    const text = (message.id === latestResidentId && residentSummary?.trim()
      ? residentSummary : message.content).trim().toLocaleLowerCase('pt-BR')
    if (seen.has(text)) return false
    seen.add(text)
    return true
  })

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
        {timelineMessages.length === 0 && <Typography color="text.secondary">Ainda não há atualizações nesta solicitação.</Typography>}
        {timelineMessages.map((message) => (
          <Box key={message.id} borderLeft="4px solid" borderColor={getUpdateMarkerColor(message)} pl={2} py={.5}>
            <Typography fontWeight={750} fontSize=".8rem">{message.isResidentReply ? 'Resposta do morador' : message.author.fullName}</Typography>
            <Typography sx={{ whiteSpace: 'pre-wrap', overflowWrap: 'anywhere' }}>{message.id === latestResidentId && residentSummary?.trim() ? residentSummary : message.content}</Typography>
            <Typography color="text.secondary" fontSize=".72rem" mt={.75}>{formatDateTime(message.createdAt)}</Typography>
          </Box>
        ))}
      </Stack>
      {readOnly || !canSendMessage(status) ? <Alert severity="info">Esta solicitação está encerrada e disponível somente para consulta.</Alert> : (
        <Box component="form" onSubmit={send}>
          {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
          <TextField fullWidth multiline minRows={3} maxRows={8} label="Descreva uma nova informação sobre a solicitação" value={content} onChange={(event) => setContent(event.target.value)} onKeyDown={handleKeyDown} inputProps={{ maxLength: 4000 }} helperText={`${content.length}/4000 · Ctrl + Enter para adicionar`} disabled={isSending} />
          <Box display="flex" justifyContent="flex-end" mt={1.5}><Button type="submit" variant="contained" disabled={!content.trim() || isSending} startIcon={isSending ? <CircularProgress size={18} color="inherit" /> : <SendRoundedIcon />}>{isSending ? 'Adicionando…' : 'Adicionar atualização'}</Button></Box>
        </Box>
      )}
    </Box>
  )
}
