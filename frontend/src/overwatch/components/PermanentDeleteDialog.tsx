import { useState } from 'react'
import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, TextField } from '@mui/material'

export function PermanentDeleteDialog({ open, displayName, contextLabel, expectedConfirmation, loading, reason, error, onConfirm, onClose }: { open: boolean; displayName: string; contextLabel?: string; expectedConfirmation: string; loading?: boolean; reason?: string | null; error?: string; onConfirm: () => void; onClose: () => void }) {
  const [confirmation, setConfirmation] = useState('')
  const close = () => { setConfirmation(''); onClose() }
  const eligible = !reason
  return <Dialog open={open} onClose={loading ? undefined : close} fullWidth maxWidth="xs"><DialogTitle>Excluir permanentemente</DialogTitle><DialogContent><Alert severity={eligible ? 'warning' : 'info'}>{eligible ? <>Excluir permanentemente <strong>{displayName}</strong>{contextLabel ? ` (${contextLabel})` : ''}? Esta ação não pode ser desfeita e só é permitida para registros sem histórico.</> : reason}</Alert>{error && <Alert severity="error" sx={{ mt: 2 }}>{error}</Alert>}{eligible && <TextField autoFocus fullWidth sx={{ mt: 2 }} label={`Digite: ${expectedConfirmation}`} value={confirmation} onChange={event => setConfirmation(event.target.value)} disabled={loading} />}</DialogContent><DialogActions><Button onClick={close} disabled={loading}>Cancelar</Button>{eligible && <Button color="error" variant="contained" disabled={loading || confirmation !== expectedConfirmation} onClick={onConfirm}>{loading ? 'Excluindo…' : 'Excluir permanentemente'}</Button>}</DialogActions></Dialog>
}
