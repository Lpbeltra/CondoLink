import { useEffect, useState, type ReactNode } from 'react'
import ExpandMoreRoundedIcon from '@mui/icons-material/ExpandMoreRounded'
import DownloadRoundedIcon from '@mui/icons-material/DownloadRounded'
import { Accordion, AccordionDetails, AccordionSummary, Alert, Box, Button, Chip, CircularProgress, Stack, Typography } from '@mui/material'
import { getRequestAttachmentBlob } from '../api'
import { formatDateTime } from '../presentation'
import type { OriginalReport } from '../types'

export function OriginalReportAccordion({ report, attachments }: {
  report: OriginalReport | null
  attachments?: ReactNode
}) {
  const [expanded, setExpanded] = useState(false)
  const [audioUrl, setAudioUrl] = useState<string | null>(null)
  const [audioBlob, setAudioBlob] = useState<Blob | null>(null)
  const [audioLoading, setAudioLoading] = useState(false)
  const [audioError, setAudioError] = useState('')

  useEffect(() => {
    if (!expanded || !report?.audioAttachment || audioUrl) return
    let active = true
    setAudioLoading(true)
    setAudioError('')
    void getRequestAttachmentBlob(report.audioAttachment.contentUrl)
      .then(blob => {
        if (!active) return
        setAudioBlob(blob)
        setAudioUrl(URL.createObjectURL(blob))
      })
      .catch(() => {
        if (active) setAudioError('Não foi possível carregar o áudio original.')
      })
      .finally(() => {
        if (active) setAudioLoading(false)
      })
    return () => { active = false }
  }, [audioUrl, expanded, report?.audioAttachment])

  useEffect(() => () => {
    if (audioUrl && typeof URL.revokeObjectURL === 'function')
      URL.revokeObjectURL(audioUrl)
  }, [audioUrl])

  if (!report) return null
  const downloadAudio = () => {
    if (!audioBlob || !report.audioAttachment) return
    const url = URL.createObjectURL(audioBlob)
    const anchor = document.createElement('a')
    anchor.href = url
    anchor.download = report.audioAttachment.originalFileName
    document.body.appendChild(anchor)
    anchor.click()
    anchor.remove()
    URL.revokeObjectURL(url)
  }
  return (
    <Accordion expanded={expanded} onChange={(_, value) => setExpanded(value)} disableGutters elevation={0} sx={{ mt: 3, border: '1px solid', borderColor: 'divider', borderRadius: '12px !important', '&::before': { display: 'none' } }}>
      <AccordionSummary expandIcon={<ExpandMoreRoundedIcon />}>
        <Typography variant="h2">Relato original do morador</Typography>
      </AccordionSummary>
      <AccordionDetails>
        <Box display="flex" flexWrap="wrap" gap={1} mb={2}>
          <Chip label="Origem: WhatsApp" size="small" variant="outlined" />
          <Typography color="text.secondary" fontSize=".8rem" alignSelf="center">{formatDateTime(report.createdAt)}</Typography>
        </Box>
        {report.audioAttachment && (
          <Stack gap={1.5} mb={report.text ? 3 : 0}>
            <Typography variant="h3">Áudio original</Typography>
            {audioLoading && <CircularProgress size={24} aria-label="Carregando áudio original" />}
            {audioError && <Alert severity="error">{audioError}</Alert>}
            {audioUrl && <Box component="audio" aria-label="Áudio original do morador" controls preload="metadata" src={audioUrl} sx={{ width: '100%' }}>Seu navegador não consegue reproduzir este áudio.</Box>}
            {audioBlob && <Button variant="text" startIcon={<DownloadRoundedIcon />} onClick={downloadAudio} sx={{ alignSelf: 'flex-start' }}>Baixar áudio</Button>}
          </Stack>
        )}
        {report.text && <Box>{report.audioAttachment && <Typography variant="h3" mb={1}>Transcrição</Typography>}<Typography sx={{ whiteSpace: 'pre-wrap', overflowWrap: 'anywhere' }}>{report.text}</Typography></Box>}
        {attachments}
      </AccordionDetails>
    </Accordion>
  )
}
