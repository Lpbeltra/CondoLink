import { useEffect, useMemo, useState } from 'react'
import ExpandMoreRoundedIcon from '@mui/icons-material/ExpandMoreRounded'
import DownloadRoundedIcon from '@mui/icons-material/DownloadRounded'
import { Accordion, AccordionDetails, AccordionSummary, Alert, Box, Button, Chip, CircularProgress, Divider, Stack, Typography } from '@mui/material'
import { getRequestAttachmentBlob, listRequestAttachments } from '../api'
import { formatDateTime } from '../presentation'
import type { OriginalReport, RequestAttachment, RequestMessage } from '../types'

interface Props {
  requestId: string
  report: OriginalReport | null
  messages: RequestMessage[]
  authorId: string
  portalDescription: string
  requestCreatedAt: string
}

const transcriptionFallback = 'Áudio enviado pelo morador.'
const originLabel = (channel: string) =>
  channel === 'Portal' ? 'Portal' : 'WhatsApp'

function AudioItem({ attachment, message }: { attachment: RequestAttachment; message?: RequestMessage }) {
  const [url, setUrl] = useState<string | null>(null)
  const [blob, setBlob] = useState<Blob | null>(null)
  const [error, setError] = useState('')

  useEffect(() => {
    let active = true
    let objectUrl: string | null = null
    void getRequestAttachmentBlob(attachment.contentUrl)
      .then(value => {
        if (!active) return
        objectUrl = URL.createObjectURL(value)
        setBlob(value)
        setUrl(objectUrl)
      })
      .catch(() => { if (active) setError('Não foi possível carregar o áudio.') })
    return () => {
      active = false
      if (objectUrl && typeof URL.revokeObjectURL === 'function') URL.revokeObjectURL(objectUrl)
    }
  }, [attachment.contentUrl])

  const download = () => {
    if (!blob) return
    const downloadUrl = URL.createObjectURL(blob)
    const anchor = document.createElement('a')
    anchor.href = downloadUrl
    anchor.download = attachment.originalFileName
    document.body.appendChild(anchor)
    anchor.click()
    anchor.remove()
    URL.revokeObjectURL(downloadUrl)
  }

  const transcription = message?.content !== transcriptionFallback ? message?.content : null
  return <Box sx={{ border: '1px solid', borderColor: 'divider', borderRadius: 2, p: 2 }}>
    <Typography color="text.secondary" fontSize=".8rem" mb={1}>{formatDateTime(message?.createdAt ?? attachment.createdAt)}</Typography>
    {!url && !error && <CircularProgress size={24} aria-label="Carregando áudio" />}
    {error && <Alert severity="error">{error}</Alert>}
    {url && <Box component="audio" aria-label={`Áudio do morador de ${formatDateTime(message?.createdAt ?? attachment.createdAt)}`} controls preload="metadata" src={url} sx={{ width: '100%' }}>Seu navegador não consegue reproduzir este áudio.</Box>}
    {blob && <Button variant="text" startIcon={<DownloadRoundedIcon />} onClick={download}>Baixar áudio</Button>}
    {transcription && <Box mt={1.5}><Typography variant="h3" mb={.5}>Transcrição</Typography><Typography sx={{ whiteSpace: 'pre-wrap', overflowWrap: 'anywhere' }}>{transcription}</Typography></Box>}
  </Box>
}

export function OriginalReportAccordion({ requestId, report, messages, authorId, portalDescription, requestCreatedAt }: Props) {
  const [expanded, setExpanded] = useState(false)
  const [attachments, setAttachments] = useState<RequestAttachment[]>([])
  const [error, setError] = useState('')

  useEffect(() => {
    if (!expanded) return
    let active = true
    void listRequestAttachments(requestId)
      .then(items => { if (active) setAttachments(items) })
      .catch(() => { if (active) setError('Não foi possível carregar o conteúdo do atendimento.') })
    return () => { active = false }
  }, [expanded, requestId])

  const audioAttachments = useMemo(() => attachments
    .filter(item => item.contentType.toLowerCase().startsWith('audio/'))
    .sort((left, right) => right.createdAt.localeCompare(left.createdAt)), [attachments])
  const audioMessageIds = useMemo(() => new Set(audioAttachments
    .map(item => item.requestMessageId).filter(Boolean)), [audioAttachments])
  const reports = useMemo(() => {
    const residentMessages = messages
      .filter(message => message.author.id === authorId && !audioMessageIds.has(message.id))
      .map(message => ({ id: message.id, text: message.content, channel: message.channel ?? 'Portal', createdAt: message.createdAt }))
    if (!report) residentMessages.unshift({ id: 'portal-opening', text: portalDescription, channel: 'Portal', createdAt: requestCreatedAt })
    return residentMessages.sort((left, right) => left.createdAt.localeCompare(right.createdAt))
  }, [audioMessageIds, authorId, messages, portalDescription, report, requestCreatedAt])

  return <Accordion expanded={expanded} onChange={(_, value) => setExpanded(value)} disableGutters elevation={0} sx={{ mt: 3, border: '1px solid', borderColor: 'divider', borderRadius: '12px !important', '&::before': { display: 'none' } }}>
    <AccordionSummary expandIcon={<ExpandMoreRoundedIcon />}><Typography variant="h2">Relatos originais do morador</Typography></AccordionSummary>
    <AccordionDetails>
      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
      <Stack gap={2}>
        {reports.map(item => <Box key={item.id}>
          <Box display="flex" flexWrap="wrap" gap={1} mb={1}>
            <Chip label={`Origem: ${originLabel(item.channel)}`} size="small" variant="outlined" />
            <Typography color="text.secondary" fontSize=".8rem" alignSelf="center">{formatDateTime(item.createdAt)}</Typography>
          </Box>
          <Typography sx={{ whiteSpace: 'pre-wrap', overflowWrap: 'anywhere' }}>{item.text}</Typography>
        </Box>)}
      </Stack>
      <Divider sx={{ my: 3 }} />
      <Typography variant="h2" mb={2}>Áudios</Typography>
      {audioAttachments.length === 0
        ? <Typography color="text.secondary">Nenhum áudio enviado.</Typography>
        : <Stack gap={1.5}>{audioAttachments.map(attachment => <AudioItem key={attachment.id} attachment={attachment} message={messages.find(message => message.id === attachment.requestMessageId)} />)}</Stack>}
    </AccordionDetails>
  </Accordion>
}
