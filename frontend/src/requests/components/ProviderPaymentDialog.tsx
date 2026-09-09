import { useState } from 'react'
import { Alert, Button, CircularProgress, Dialog, DialogActions, DialogContent, DialogTitle, MenuItem, Stack, TextField, Typography } from '@mui/material'
import { CurrencyField } from '../../components/CurrencyField'
import { createProviderPaymentRequest } from '../api'
import type { ServiceProvider } from '../types'
import { selectAttachmentFiles } from '../attachments'
import { LocalAttachmentsPreview } from '../../managementCompanyRequests/LocalAttachmentsPreview'

const today = () => new Date().toISOString().slice(0, 10)
const pixTypes = [['Cpf', 'CPF'], ['Cnpj', 'CNPJ'], ['Email', 'E-mail'], ['Phone', 'Telefone'], ['Random', 'Chave aleatória']] as const

export function ProviderPaymentDialog({ requestId, provider, requestTitle, condominium, unit, onClose, onCreated }: { requestId: string; provider: ServiceProvider; requestTitle: string; condominium?: string; unit?: string | null; onClose: () => void; onCreated: (identifier: string) => Promise<void> | void }) {
  const [value, setValue] = useState<number | null>(null), [nature, setNature] = useState(`Pagamento de prestador — ${provider.name}`), [notes, setNotes] = useState('')
  const [pixKey, setPixKey] = useState(provider.pixKey ?? ''), [pixKeyType, setPixKeyType] = useState(provider.pixKeyType ?? ''), [dueDate, setDueDate] = useState(today()), [files, setFiles] = useState<File[]>([])
  const [saving, setSaving] = useState(false), [error, setError] = useState('')
  const submit = async () => {
    if (value === null || !nature.trim() || !pixKey.trim() || !pixKeyType) { setError('Informe valor, descrição, tipo e chave PIX.'); return }
    setSaving(true); setError('')
    try {
      const created = await createProviderPaymentRequest(requestId, { nature, value, eventDate: today(), dueDate, notes: notes || null, pixKeyType, pixKey }, files)
      await onCreated(created.friendlyIdentifier); onClose()
    } catch { setError('Não foi possível solicitar o pagamento. Confirme a configuração da administradora e tente novamente.') }
    finally { setSaving(false) }
  }
  return <Dialog open onClose={saving ? undefined : onClose} fullWidth maxWidth="sm">
    <DialogTitle>Solicitar pagamento à administradora</DialogTitle>
    <DialogContent><Stack spacing={2} mt={.5}>
      <Typography color="text.secondary">Prestador: <b>{provider.name}</b>{provider.companyName ? ` · ${provider.companyName}` : ''}<br />Especialidade: {provider.specialties?.join(', ') || provider.specialty}<br />Atendimento: {requestTitle}{condominium ? ` · ${condominium}` : ''}{unit ? ` · ${unit}` : ''}</Typography>
      {error && <Alert severity="error">{error}</Alert>}
      <CurrencyField required label="Valor" value={value} onValueChange={setValue} disabled={saving} />
      <TextField required label="Descrição / justificativa" value={nature} onChange={e => setNature(e.target.value)} disabled={saving} inputProps={{ maxLength: 500 }} />
      <TextField required type="date" label="Vencimento" value={dueDate} onChange={e => setDueDate(e.target.value)} disabled={saving} InputLabelProps={{ shrink: true }} />
      <TextField select required label="Tipo da chave PIX" value={pixKeyType} onChange={e => setPixKeyType(e.target.value)} disabled={saving}>{pixTypes.map(([value, label]) => <MenuItem key={value} value={value}>{label}</MenuItem>)}</TextField>
      <TextField required label="Chave PIX" value={pixKey} onChange={e => setPixKey(e.target.value)} disabled={saving} inputProps={{ maxLength: 200 }} helperText="Esta alteração vale somente para esta solicitação." />
      <TextField multiline minRows={3} label="Observações" value={notes} onChange={e => setNotes(e.target.value)} disabled={saving} inputProps={{ maxLength: 4000 }} />
      <Button component="label" variant="outlined" disabled={saving}>Selecionar anexos<input hidden multiple type="file" onChange={e => { const result = selectAttachmentFiles(files, Array.from(e.target.files ?? [])); setFiles(result.files); setError(result.error ?? '') }} /></Button>
      <LocalAttachmentsPreview files={files} onRemove={index => setFiles(current => current.filter((_, i) => i !== index))} />
    </Stack></DialogContent>
    <DialogActions><Button disabled={saving} onClick={onClose}>Cancelar</Button><Button variant="contained" disabled={saving} onClick={() => void submit()}>{saving ? <CircularProgress size={20} color="inherit" /> : 'Solicitar pagamento'}</Button></DialogActions>
  </Dialog>
}
