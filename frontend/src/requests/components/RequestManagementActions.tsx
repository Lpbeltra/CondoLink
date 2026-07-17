import { useState } from 'react'
import axios from 'axios'
import EditRoundedIcon from '@mui/icons-material/EditRounded'
import ReplayRoundedIcon from '@mui/icons-material/ReplayRounded'
import CheckCircleOutlineRoundedIcon from '@mui/icons-material/CheckCircleOutlineRounded'
import CancelOutlinedIcon from '@mui/icons-material/CancelOutlined'
import { Alert, Box, Button, CircularProgress, Dialog, DialogActions, DialogContent, DialogTitle, FormControl, InputLabel, MenuItem, Paper, Select, Stack, TextField, Typography } from '@mui/material'
import { updateRequestPriority, updateRequestStatus } from '../api'
import { allowedStatusTransitions, priorityPresentation, statusPresentation } from '../presentation'
import type { RequestPriority, RequestStatus } from '../types'
import { canSubmitStatus, getRequestActionVisibility, getStatusConfirmation, requestShortcutStatuses } from '../requestActions'

interface Props { requestId: string; status: RequestStatus; priority: RequestPriority; onUpdated: () => Promise<void> }

export function RequestManagementActions({ requestId, status, priority, onUpdated }: Props) {
  const [statusOpen, setStatusOpen] = useState(false)
  const [priorityOpen, setPriorityOpen] = useState(false)
  const [nextStatus, setNextStatus] = useState<RequestStatus | ''>('')
  const [nextPriority, setNextPriority] = useState<RequestPriority | ''>('')
  const [reason, setReason] = useState('')
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')
  const [isSaving, setIsSaving] = useState(false)
  const [shortcut, setShortcut] = useState<RequestStatus | null>(null)
  const transitions = allowedStatusTransitions[status]
  const actions = getRequestActionVisibility(status)

  const friendlyError = (requestError: unknown) => axios.isAxiosError(requestError) && requestError.response?.status === 409
    ? 'Esta alteração não é mais válida. Atualize os dados e tente novamente.'
    : 'Não foi possível salvar a alteração.'

  const saveStatus = async () => {
    if (!canSubmitStatus(nextStatus, isSaving)) return
    setIsSaving(true); setError(''); setSuccess('')
    try { const changedStatus = nextStatus; await updateRequestStatus(requestId, changedStatus, reason.trim() || null); setStatusOpen(false); setNextStatus(''); setShortcut(null); setReason(''); await onUpdated(); setSuccess(changedStatus === 'Open' ? 'Solicitação reaberta com sucesso.' : changedStatus === 'Resolved' ? 'Solicitação resolvida com sucesso.' : changedStatus === 'Cancelled' ? 'Solicitação cancelada com sucesso.' : 'Status atualizado com sucesso.') }
    catch (requestError) { setError(friendlyError(requestError)) }
    finally { setIsSaving(false) }
  }

  const savePriority = async () => {
    if (!nextPriority || nextPriority === priority || isSaving) return
    setIsSaving(true); setError(''); setSuccess('')
    try { await updateRequestPriority(requestId, nextPriority); setPriorityOpen(false); setNextPriority(''); await onUpdated(); setSuccess('Prioridade atualizada com sucesso.') }
    catch (requestError) { setError(friendlyError(requestError)) }
    finally { setIsSaving(false) }
  }

  return <Paper elevation={0} sx={{ mt: 3, p: { xs: 2.5, sm: 3 }, border: '1px solid', borderColor: 'rgba(114,89,217,.25)', bgcolor: 'rgba(114,89,217,.035)' }}>
    <Typography variant="h3">Ações de atendimento</Typography><Typography color="text.secondary" mt={.5}>Atualize a situação desta solicitação.</Typography>
    {success && <Alert severity="success" sx={{ mt: 2 }}>{success}</Alert>}
    <Box display="grid" gridTemplateColumns={{ xs: '1fr', sm: 'repeat(2, minmax(0, 1fr))', md: actions.reopen ? 'minmax(220px, max-content)' : 'repeat(4, minmax(0, 1fr))' }} gap={1.5} mt={2.5}>
      {(actions.changeStatus || actions.reopen) && <Button variant="contained" color="secondary" startIcon={actions.reopen ? <ReplayRoundedIcon /> : <EditRoundedIcon />} disabled={transitions.length === 0 || isSaving} onClick={() => { setError(''); setSuccess(''); setShortcut(actions.reopen ? 'Open' : null); if (actions.reopen) setNextStatus('Open'); setStatusOpen(true) }}>{actions.reopen ? 'Reabrir solicitação' : 'Alterar status'}</Button>}
      {actions.changePriority && <Button variant="outlined" color="secondary" disabled={isSaving} onClick={() => { setError(''); setSuccess(''); setPriorityOpen(true) }}>Alterar prioridade</Button>}
      {actions.resolve && <Button variant="contained" color="success" startIcon={<CheckCircleOutlineRoundedIcon />} disabled={isSaving} onClick={() => { setError(''); setSuccess(''); setShortcut(requestShortcutStatuses.resolve); setNextStatus(requestShortcutStatuses.resolve); setStatusOpen(true) }}>Resolver</Button>}
      {actions.cancel && <Button variant="contained" color="error" startIcon={<CancelOutlinedIcon />} disabled={isSaving} onClick={() => { setError(''); setSuccess(''); setShortcut(requestShortcutStatuses.cancel); setNextStatus(requestShortcutStatuses.cancel); setStatusOpen(true) }}>Cancelar</Button>}
    </Box>

    <Dialog open={statusOpen} onClose={() => { if (!isSaving) { setStatusOpen(false); setShortcut(null); setNextStatus('') } }} fullWidth maxWidth="xs">
      <DialogTitle>{shortcut === 'Open' ? 'Reabrir solicitação' : shortcut === 'Resolved' ? 'Resolver solicitação' : shortcut === 'Cancelled' ? 'Cancelar solicitação' : 'Alterar status'}</DialogTitle><DialogContent><Stack spacing={2} mt={1}>{error && <Alert severity="error">{error}</Alert>}{shortcut === 'Open' ? <Alert severity="info">A solicitação voltará para os atendimentos ativos com o status Aberta.</Alert> : shortcut ? <Typography>{getStatusConfirmation(shortcut)}</Typography> : <FormControl fullWidth><InputLabel>Novo status</InputLabel><Select label="Novo status" value={nextStatus} onChange={(event) => setNextStatus(event.target.value as RequestStatus)}>{transitions.map((item) => <MenuItem key={item} value={item}>{statusPresentation[item].label}</MenuItem>)}</Select></FormControl>}<TextField multiline minRows={3} label="Motivo (opcional)" value={reason} onChange={(event) => setReason(event.target.value)} inputProps={{ maxLength: 500 }} helperText={`${reason.length}/500`} />{!shortcut && nextStatus === 'Cancelled' && <Alert severity="warning">Confirme o encerramento desta solicitação.</Alert>}</Stack></DialogContent>
      <DialogActions><Button onClick={() => { setStatusOpen(false); setShortcut(null); setNextStatus('') }} disabled={isSaving}>Voltar</Button><Button variant="contained" color={nextStatus === 'Cancelled' ? 'error' : nextStatus === 'Resolved' ? 'success' : 'secondary'} disabled={!canSubmitStatus(nextStatus, isSaving)} onClick={() => void saveStatus()}>{isSaving ? <CircularProgress size={20} color="inherit" /> : shortcut === 'Open' ? 'Confirmar reabertura' : shortcut === 'Resolved' ? 'Confirmar resolução' : shortcut === 'Cancelled' ? 'Confirmar cancelamento' : 'Confirmar'}</Button></DialogActions>
    </Dialog>

    <Dialog open={priorityOpen} onClose={() => !isSaving && setPriorityOpen(false)} fullWidth maxWidth="xs">
      <DialogTitle>Alterar prioridade</DialogTitle><DialogContent><Box mt={1}>{error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}<FormControl fullWidth><InputLabel>Nova prioridade</InputLabel><Select label="Nova prioridade" value={nextPriority} onChange={(event) => setNextPriority(event.target.value as RequestPriority)}>{(['Normal', 'High', 'Urgent'] as RequestPriority[]).filter((item) => item !== priority).map((item) => <MenuItem key={item} value={item}>{priorityPresentation[item].label}</MenuItem>)}</Select></FormControl></Box></DialogContent>
      <DialogActions><Button onClick={() => setPriorityOpen(false)} disabled={isSaving}>Voltar</Button><Button variant="contained" color="secondary" disabled={!nextPriority || isSaving} onClick={() => void savePriority()}>{isSaving ? <CircularProgress size={20} color="inherit" /> : 'Salvar'}</Button></DialogActions>
    </Dialog>
  </Paper>
}
