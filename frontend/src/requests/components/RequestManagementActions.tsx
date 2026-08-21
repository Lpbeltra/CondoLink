import { useState } from 'react'
import axios from 'axios'
import AutoAwesomeRoundedIcon from '@mui/icons-material/AutoAwesomeRounded'
import EditRoundedIcon from '@mui/icons-material/EditRounded'
import ReplayRoundedIcon from '@mui/icons-material/ReplayRounded'
import CheckCircleOutlineRoundedIcon from '@mui/icons-material/CheckCircleOutlineRounded'
import CancelOutlinedIcon from '@mui/icons-material/CancelOutlined'
import { Alert, Box, Button, CircularProgress, Dialog, DialogActions, DialogContent, DialogTitle, FormControl, InputLabel, MenuItem, Paper, Select, Stack, TextField, Typography } from '@mui/material'
import { suggestRequestStatusMessage, updateRequestPriority, updateRequestStatus } from '../api'
import { allowedStatusTransitions, priorityPresentation, statusPresentation } from '../presentation'
import type { RequestPriority, RequestStatus } from '../types'
import { canSubmitStatus, getRequestActionVisibility, getStatusConfirmation, requestShortcutStatuses } from '../requestActions'

interface Props { requestId: string; status: RequestStatus; priority: RequestPriority; onUpdated: () => Promise<void> }
const residentStatuses: RequestStatus[] = ['WaitingForResident', 'WaitingForThirdParty', 'WaitingForResidentClosure', 'Resolved', 'Cancelled', 'Open']

export function RequestManagementActions({ requestId, status, priority, onUpdated }: Props) {
  const [statusOpen, setStatusOpen] = useState(false), [priorityOpen, setPriorityOpen] = useState(false)
  const [nextStatus, setNextStatus] = useState<RequestStatus | ''>(''), [nextPriority, setNextPriority] = useState<RequestPriority | ''>('')
  const [reason, setReason] = useState(''), [suggestion, setSuggestion] = useState(''), [suggestionSource, setSuggestionSource] = useState('')
  const [error, setError] = useState(''), [suggestionError, setSuggestionError] = useState(''), [success, setSuccess] = useState('')
  const [isSaving, setIsSaving] = useState(false), [isSuggesting, setIsSuggesting] = useState(false)
  const [shortcut, setShortcut] = useState<RequestStatus | null>(null)
  const transitions = allowedStatusTransitions[status], actions = getRequestActionVisibility(status)
  const canSuggest = !!nextStatus && residentStatuses.includes(nextStatus) && !!reason.trim()
  const suggestionIsStale = !!suggestion && suggestionSource !== reason

  const friendlyError = (requestError: unknown) => {
    if (axios.isAxiosError(requestError) && requestError.response?.status === 409) return 'Esta alteração não é mais válida. Atualize os dados e tente novamente.'
    return (axios.isAxiosError<{ error?: string }>(requestError) && requestError.response?.data?.error) || 'Não foi possível salvar a alteração.'
  }
  const resetComposer = () => { setReason(''); setSuggestion(''); setSuggestionSource(''); setSuggestionError('') }
  const closeStatus = () => { setStatusOpen(false); setShortcut(null); setNextStatus(''); resetComposer() }
  const openStatus = (selected: RequestStatus | null) => { setError(''); setSuccess(''); resetComposer(); setShortcut(selected); if (selected) setNextStatus(selected); setStatusOpen(true) }

  const saveStatus = async (message: string) => {
    if (!canSubmitStatus(nextStatus, isSaving) || message.length > 1000) return
    setIsSaving(true); setError(''); setSuccess('')
    try {
      const changedStatus = nextStatus
      await updateRequestStatus(requestId, changedStatus, message.trim() || null)
      closeStatus(); await onUpdated()
      setSuccess(changedStatus === 'Open' ? 'Solicitação reaberta com sucesso.' : changedStatus === 'Resolved' ? 'Conclusão enviada ao morador.' : changedStatus === 'Cancelled' ? 'Solicitação cancelada com sucesso.' : 'Status atualizado com sucesso.')
    } catch (requestError) { setError(friendlyError(requestError)) } finally { setIsSaving(false) }
  }
  const generateSuggestion = async () => {
    if (!canSuggest || isSuggesting || reason.length > 1000 || !nextStatus) return
    const source = reason; setIsSuggesting(true); setSuggestionError('')
    try { const result = await suggestRequestStatusMessage(requestId, nextStatus, source.trim()); setSuggestion(result.suggestion); setSuggestionSource(source) }
    catch { setSuggestionError('Não foi possível gerar a sugestão. Você ainda pode enviar seu texto.') }
    finally { setIsSuggesting(false) }
  }
  const savePriority = async () => {
    if (!nextPriority || nextPriority === priority || isSaving) return
    setIsSaving(true); setError(''); setSuccess('')
    try { await updateRequestPriority(requestId, nextPriority); setPriorityOpen(false); setNextPriority(''); await onUpdated(); setSuccess('Prioridade atualizada com sucesso.') }
    catch (requestError) { setError(friendlyError(requestError)) } finally { setIsSaving(false) }
  }
  const counterColor = reason.length >= 900 ? (reason.length > 1000 ? 'error.main' : 'warning.main') : 'text.secondary'

  return <Paper elevation={0} sx={{ mt: 3, p: { xs: 2.5, sm: 3 }, border: '1px solid', borderColor: 'rgba(114,89,217,.25)', bgcolor: 'rgba(114,89,217,.035)' }}>
    <Typography variant="h3">Ações de atendimento</Typography><Typography color="text.secondary" mt={.5}>Atualize a situação desta solicitação.</Typography>
    {success && <Alert severity="success" sx={{ mt: 2 }}>{success}</Alert>}
    <Box display="grid" gridTemplateColumns={{ xs: '1fr', sm: 'repeat(2, minmax(0, 1fr))', md: actions.reopen ? 'minmax(220px, max-content)' : 'repeat(4, minmax(0, 1fr))' }} gap={1.5} mt={2.5}>
      {(actions.changeStatus || actions.reopen) && <Button variant="contained" color="secondary" startIcon={actions.reopen ? <ReplayRoundedIcon /> : <EditRoundedIcon />} disabled={transitions.length === 0 || isSaving} onClick={() => openStatus(actions.reopen ? 'Open' : null)}>{actions.reopen ? 'Reabrir solicitação' : 'Alterar status'}</Button>}
      {actions.changePriority && <Button variant="outlined" color="secondary" disabled={isSaving} onClick={() => { setError(''); setSuccess(''); setPriorityOpen(true) }}>Alterar prioridade</Button>}
      {actions.resolve && <Button variant="contained" color="success" startIcon={<CheckCircleOutlineRoundedIcon />} disabled={isSaving} onClick={() => openStatus(requestShortcutStatuses.resolve)}>Resolver</Button>}
      {actions.cancel && <Button variant="contained" color="error" startIcon={<CancelOutlinedIcon />} disabled={isSaving} onClick={() => openStatus(requestShortcutStatuses.cancel)}>Cancelar</Button>}
    </Box>

    <Dialog open={statusOpen} onClose={() => { if (!isSaving && !isSuggesting) closeStatus() }} fullWidth maxWidth="sm">
      <DialogTitle>{shortcut === 'Open' ? 'Reabrir solicitação' : shortcut === 'Resolved' ? 'Resolver solicitação' : shortcut === 'Cancelled' ? 'Cancelar solicitação' : 'Alterar status'}</DialogTitle>
      <DialogContent><Stack spacing={2} mt={1}>
        {error && <Alert severity="error">{error}</Alert>}
        {shortcut === 'Open' ? <Alert severity="info">A solicitação voltará para os atendimentos ativos com o status Aberta.</Alert> : shortcut ? <Typography>{getStatusConfirmation(shortcut)}</Typography> : <FormControl fullWidth><InputLabel>Novo status</InputLabel><Select label="Novo status" value={nextStatus} onChange={event => { setNextStatus(event.target.value as RequestStatus); setSuggestion(''); setSuggestionSource('') }}>{transitions.map(item => <MenuItem key={item} value={item}>{statusPresentation[item].label}</MenuItem>)}</Select></FormControl>}
        <TextField multiline minRows={3} label="Mensagem ao morador (opcional)" value={reason} onChange={event => setReason(event.target.value)} inputProps={{ maxLength: 1001 }} error={reason.length > 1000} helperText={<Box component="span" display="flex" justifyContent="space-between"><span>{reason.length > 1000 ? 'A mensagem pode ter no máximo 1000 caracteres.' : 'Você pode enviar este texto diretamente.'}</span><Box component="span" color={counterColor}>{reason.length} / 1000</Box></Box>} />
        {canSuggest && <Box><Button size="small" startIcon={isSuggesting ? <CircularProgress size={16} /> : <AutoAwesomeRoundedIcon />} disabled={isSuggesting || isSaving || reason.length > 1000} onClick={() => void generateSuggestion()}>Gerar sugestão com IA</Button></Box>}
        {suggestionError && <Alert severity="warning">{suggestionError}</Alert>}
        {suggestion && <Stack spacing={1.5}>
          <Box><Typography variant="subtitle2">Seu texto</Typography><Paper variant="outlined" sx={{ p: 1.5, mt: .5, whiteSpace: 'pre-wrap' }}>{reason}</Paper></Box>
          <Box><Typography variant="subtitle2">Sugestão da IA</Typography><TextField fullWidth multiline minRows={3} value={suggestion} onChange={event => setSuggestion(event.target.value)} inputProps={{ maxLength: 1000 }} helperText={`${suggestion.length} / 1000`} /></Box>
          {suggestionIsStale && <Alert severity="warning">A sugestão foi gerada a partir de uma versão anterior do seu texto. Gere novamente para atualizá-la.</Alert>}
        </Stack>}
        {!shortcut && nextStatus === 'Cancelled' && <Alert severity="warning">Confirme o encerramento desta solicitação.</Alert>}
      </Stack></DialogContent>
      <DialogActions sx={{ flexWrap: 'wrap' }}><Button onClick={closeStatus} disabled={isSaving || isSuggesting}>Voltar</Button>{suggestion && <Button variant="outlined" disabled={isSaving || !suggestion.trim() || suggestionIsStale} onClick={() => void saveStatus(suggestion)}>Enviar sugestão da IA</Button>}<Button variant="contained" color={nextStatus === 'Cancelled' ? 'error' : 'secondary'} disabled={!canSubmitStatus(nextStatus, isSaving) || isSuggesting || reason.length > 1000} onClick={() => void saveStatus(reason)}>{isSaving ? <CircularProgress size={20} color="inherit" /> : suggestion ? 'Enviar meu texto' : shortcut === 'Open' ? 'Confirmar reabertura' : shortcut === 'Resolved' ? 'Enviar conclusão' : shortcut === 'Cancelled' ? 'Confirmar cancelamento' : 'Confirmar'}</Button></DialogActions>
    </Dialog>

    <Dialog open={priorityOpen} onClose={() => !isSaving && setPriorityOpen(false)} fullWidth maxWidth="xs"><DialogTitle>Alterar prioridade</DialogTitle><DialogContent><Box mt={1}>{error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}<FormControl fullWidth><InputLabel>Nova prioridade</InputLabel><Select label="Nova prioridade" value={nextPriority} onChange={event => setNextPriority(event.target.value as RequestPriority)}>{(['Normal', 'High', 'Urgent'] as RequestPriority[]).filter(item => item !== priority).map(item => <MenuItem key={item} value={item}>{priorityPresentation[item].label}</MenuItem>)}</Select></FormControl></Box></DialogContent><DialogActions><Button onClick={() => setPriorityOpen(false)} disabled={isSaving}>Voltar</Button><Button variant="contained" color="secondary" disabled={!nextPriority || isSaving} onClick={() => void savePriority()}>{isSaving ? <CircularProgress size={20} color="inherit" /> : 'Salvar'}</Button></DialogActions></Dialog>
  </Paper>
}
