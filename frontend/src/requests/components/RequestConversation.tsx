import { WhatsAppDeliveryIndicator } from './WhatsAppDeliveryIndicator'
import { useState, type FormEvent, type KeyboardEvent } from 'react'
import SendRoundedIcon from '@mui/icons-material/SendRounded'
import { Alert, Box, Button, CircularProgress, Stack, TextField, Typography } from '@mui/material'
import { createRequestMessage } from '../api'
import { canSendMessage, formatDateTime, getRequestError } from '../presentation'
import { getUpdateMarkerColor } from '../requestUpdates'
import type { RequestMessage, RequestStatus } from '../types'

interface Props { requestId: string; status: RequestStatus; messages: RequestMessage[]; onMessageCreated: (message: RequestMessage) => void; readOnly?: boolean; showComposer?: boolean }

export function RequestConversation({ requestId, status, messages, onMessageCreated, readOnly = false, showComposer = true }: Props) {
  const [content, setContent] = useState('')
  const [error, setError] = useState('')
  const [isSending, setIsSending] = useState(false)
  const orderedMessages = [...messages]
    .filter(message => !message.isAdministrativeEvent
      && ['Portal', 'WhatsApp', 'WhatsAppResidentUpdate'].includes(message.channel ?? 'Portal'))
    .sort((left, right) =>
    left.createdAt.localeCompare(right.createdAt) || left.id.localeCompare(right.id))

  const send = async (event?: FormEvent) => {
    event?.preventDefault()
    const trimmed = content.trim()
    if (!trimmed || trimmed.length > 3000 || isSending) return
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
        {orderedMessages.length === 0 && <Typography color="text.secondary">Ainda não há mensagens neste atendimento.</Typography>}
        {orderedMessages.map((message) => (
          <Box key={message.id} component="article" aria-label={`Mensagem ${message.author.isManager ? 'da gestão' : 'do morador'}`}
            sx={{ borderLeft: '3px solid', borderColor: getUpdateMarkerColor(message), borderRadius: 1,
              bgcolor: message.author.isManager ? 'action.hover' : 'background.paper', p: { xs: 1.5, sm: 2 }, minWidth: 0 }}>
            <Typography fontWeight={750} fontSize=".8rem">{message.author.isManager ? 'Gestão' : 'Morador'} · {message.author.fullName}</Typography>
            <Typography sx={{ whiteSpace: 'pre-wrap', overflowWrap: 'anywhere', lineHeight: 1.7, my: 1 }}>{message.content}</Typography>
            <Typography color="text.secondary" fontSize=".72rem">{formatDateTime(message.createdAt)} <WhatsAppDeliveryIndicator delivery={message.whatsAppDelivery} /></Typography>
          </Box>
        ))}
      </Stack>
      {!showComposer ? null : readOnly || !canSendMessage(status) ? <Alert severity="info">Atualizações disponíveis somente para consulta.</Alert> : (
        <Box component="form" onSubmit={send}>
          {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
          <TextField fullWidth multiline minRows={3} maxRows={8} label="Descreva uma nova informação sobre a solicitação" value={content} onChange={(event) => setContent(event.target.value)} onKeyDown={handleKeyDown} inputProps={{ maxLength: 3001 }} error={content.length > 3000} helperText={`${content.length}/3000 · Ctrl + Enter para adicionar`} disabled={isSending} />
          <Box display="flex" justifyContent="flex-end" mt={1.5}><Button type="submit" variant="contained" disabled={!content.trim() || isSending} startIcon={isSending ? <CircularProgress size={18} color="inherit" /> : <SendRoundedIcon />}>{isSending ? 'Adicionando…' : 'Adicionar atualização'}</Button></Box>
        </Box>
      )}
    </Box>
  )
}
