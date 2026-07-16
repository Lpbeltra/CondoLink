import { useState } from 'react'
import axios from 'axios'
import EditRoundedIcon from '@mui/icons-material/EditRounded'
import { Alert, Box, Button, CircularProgress, Dialog, DialogActions, DialogContent, DialogTitle, FormControl, InputLabel, MenuItem, Paper, Select, Stack, TextField, Typography } from '@mui/material'
import { updateRequestPriority, updateRequestStatus } from '../api'
import { allowedStatusTransitions, priorityPresentation, statusPresentation } from '../presentation'
import type { RequestPriority, RequestStatus } from '../types'

interface Props { requestId: string; status: RequestStatus; priority: RequestPriority; onUpdated: () => Promise<void> }

export function RequestManagementActions({ requestId, status, priority, onUpdated }: Props) {
  const [statusOpen, setStatusOpen] = useState(false)
  const [priorityOpen, setPriorityOpen] = useState(false)
  const [nextStatus, setNextStatus] = useState<RequestStatus | ''>('')
  const [nextPriority, setNextPriority] = useState<RequestPriority | ''>('')
  const [reason, setReason] = useState('')
  const [error, setError] = useState('')
  const [isSaving, setIsSaving] = useState(false)
  const transitions = allowedStatusTransitions[status]

  const friendlyError = (requestError: unknown) => axios.isAxiosError(requestError) && requestError.response?.status === 409
    ? 'Esta alteração não é mais válida. Atualize os dados e tente novamente.'
    : 'Não foi possível salvar a alteração.'

  const saveStatus = async () => {
    if (!nextStatus || isSaving) return
    setIsSaving(true); setError('')
    try { await updateRequestStatus(requestId, nextStatus, reason.trim() || null); setStatusOpen(false); setNextStatus(''); setReason(''); await onUpdated() }
    catch (requestError) { setError(friendlyError(requestError)) }
    finally { setIsSaving(false) }
  }

  const savePriority = async () => {
    if (!nextPriority || nextPriority === priority || isSaving) return
    setIsSaving(true); setError('')
    try { await updateRequestPriority(requestId, nextPriority); setPriorityOpen(false); setNextPriority(''); await onUpdated() }
    catch (requestError) { setError(friendlyError(requestError)) }
    finally { setIsSaving(false) }
  }

  return <Paper elevation={0} sx={{ mt: 3, p: { xs: 2.5, sm: 3 }, border: '1px solid', borderColor: 'rgba(114,89,217,.25)', bgcolor: 'rgba(114,89,217,.035)' }}>
    <Typography variant="h3">Ações de atendimento</Typography><Typography color="text.secondary" mt={.5}>Atualize a situação desta solicitação.</Typography>
    <Stack direction={{ xs: 'column', sm: 'row' }} gap={1.5} mt={2.5}>
      <Button variant="contained" color="secondary" startIcon={<EditRoundedIcon />} disabled={transitions.length === 0} onClick={() => { setError(''); setStatusOpen(true) }}>Alterar status</Button>
      <Button variant="outlined" color="secondary" disabled={status === 'Cancelled'} onClick={() => { setError(''); setPriorityOpen(true) }}>Alterar prioridade</Button>
    </Stack>

    <Dialog open={statusOpen} onClose={() => !isSaving && setStatusOpen(false)} fullWidth maxWidth="xs">
      <DialogTitle>Alterar status</DialogTitle><DialogContent><Stack spacing={2} mt={1}>{error && <Alert severity="error">{error}</Alert>}<FormControl fullWidth><InputLabel>Novo status</InputLabel><Select label="Novo status" value={nextStatus} onChange={(event) => setNextStatus(event.target.value as RequestStatus)}>{transitions.map((item) => <MenuItem key={item} value={item}>{statusPresentation[item].label}</MenuItem>)}</Select></FormControl><TextField multiline minRows={3} label="Motivo (opcional)" value={reason} onChange={(event) => setReason(event.target.value)} inputProps={{ maxLength: 500 }} helperText={`${reason.length}/500`} />{nextStatus === 'Cancelled' && <Alert severity="warning">Confirme o cancelamento. Esta solicitação não poderá ser reaberta.</Alert>}</Stack></DialogContent>
      <DialogActions><Button onClick={() => setStatusOpen(false)} disabled={isSaving}>Voltar</Button><Button variant="contained" color={nextStatus === 'Cancelled' ? 'error' : 'secondary'} disabled={!nextStatus || isSaving} onClick={() => void saveStatus()}>{isSaving ? <CircularProgress size={20} color="inherit" /> : 'Confirmar'}</Button></DialogActions>
    </Dialog>

    <Dialog open={priorityOpen} onClose={() => !isSaving && setPriorityOpen(false)} fullWidth maxWidth="xs">
      <DialogTitle>Alterar prioridade</DialogTitle><DialogContent><Box mt={1}>{error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}<FormControl fullWidth><InputLabel>Nova prioridade</InputLabel><Select label="Nova prioridade" value={nextPriority} onChange={(event) => setNextPriority(event.target.value as RequestPriority)}>{(['Normal', 'High', 'Urgent'] as RequestPriority[]).filter((item) => item !== priority).map((item) => <MenuItem key={item} value={item}>{priorityPresentation[item].label}</MenuItem>)}</Select></FormControl></Box></DialogContent>
      <DialogActions><Button onClick={() => setPriorityOpen(false)} disabled={isSaving}>Voltar</Button><Button variant="contained" color="secondary" disabled={!nextPriority || isSaving} onClick={() => void savePriority()}>{isSaving ? <CircularProgress size={20} color="inherit" /> : 'Salvar'}</Button></DialogActions>
    </Dialog>
  </Paper>
}
