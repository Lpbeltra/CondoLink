import { useState } from 'react'
import { Alert, Button, Card, CardContent, CircularProgress, Stack, TextField, Typography } from '@mui/material'
import axios from 'axios'
import { confirmResidentClosure, questionResidentClosure } from '../api'
import { formatDateTime, getRequestError } from '../presentation'
import type { ResidentClosureProposal } from '../types'

interface Props {
  requestId: string
  proposal: ResidentClosureProposal
  onUpdated: (feedback?: string) => Promise<void> | void
}

export function ResidentClosurePanel({ requestId, proposal, onUpdated }: Props) {
  const [questioning, setQuestioning] = useState(false)
  const [message, setMessage] = useState('')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')

  async function run(action: () => Promise<unknown>, feedback: string) {
    if (saving) return
    setSaving(true); setError('')
    try {
      await action()
      setSuccess(feedback)
      await onUpdated(feedback)
    } catch (requestError) {
      if (axios.isAxiosError(requestError) && requestError.response?.status === 409) {
        setError('Este atendimento já foi atualizado.')
        await onUpdated()
      } else setError(getRequestError(requestError, 'Não foi possível atualizar o atendimento.'))
    } finally { setSaving(false) }
  }

  return <Card elevation={0} sx={{ mt: 3, border: 1, borderColor: 'warning.light' }}>
    <CardContent sx={{ p: { xs: 2.5, sm: 4 } }}>
      <Stack gap={2}>
        <Typography variant="h2">A administração concluiu este atendimento</Typography>
        <Typography sx={{ whiteSpace: 'pre-wrap' }}>{proposal.conclusion}</Typography>
        <Typography color="text.secondary" fontSize=".85rem">Conclusão registrada em {formatDateTime(proposal.requestedAt)}.</Typography>
        <Alert severity="info">Se não houver manifestação, o atendimento será finalizado automaticamente.</Alert>
        {error && <Alert severity="warning">{error}</Alert>}
        {success && <Alert severity="success">{success}</Alert>}
        {!questioning ? <Stack direction={{ xs: 'column', sm: 'row' }} gap={1.5}>
          <Button size="large" variant="contained" disabled={saving} onClick={() => void run(() => confirmResidentClosure(requestId), 'Atendimento finalizado. Obrigado pela confirmação.')}>
            {saving ? <CircularProgress size={20} color="inherit" /> : 'Concordar e finalizar'}
          </Button>
          <Button size="large" variant="outlined" disabled={saving} onClick={() => setQuestioning(true)}>Ainda tenho uma dúvida</Button>
        </Stack> : <Stack gap={1.5}>
          <TextField autoFocus fullWidth multiline minRows={3} maxRows={8} label="Escreva sua dúvida ou observação" value={message} onChange={event => setMessage(event.target.value)} inputProps={{ maxLength: 3001 }} helperText={`${message.length}/3000`} disabled={saving} />
          <Stack direction={{ xs: 'column-reverse', sm: 'row' }} justifyContent="flex-end" gap={1}>
            <Button disabled={saving} onClick={() => { setQuestioning(false); setMessage('') }}>Cancelar</Button>
            <Button variant="contained" disabled={saving || !message.trim()} onClick={() => void run(() => questionResidentClosure(requestId, message.trim()), 'Sua mensagem foi enviada e o atendimento voltou para análise da administração.')}>
              {saving ? 'Enviando...' : 'Enviar dúvida'}
            </Button>
          </Stack>
        </Stack>}
      </Stack>
    </CardContent>
  </Card>
}
