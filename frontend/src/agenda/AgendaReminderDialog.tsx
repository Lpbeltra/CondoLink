import { useEffect, useState } from 'react'
import { Autocomplete, Box, Button, Checkbox, Dialog, DialogActions, DialogContent, DialogTitle, FormControlLabel, MenuItem, Stack, TextField } from '@mui/material'
import { UnitAutocomplete } from '../management/components/UnitAutocomplete'
import type { Unit } from '../management/types'
import { getAgendaOptions, saveAgendaReminder } from './api'
import type { AgendaInput, AgendaReminder, AgendaRequestOption, AgendaRecurrence } from './types'

interface Props { open: boolean; condominiumId: string; reminder?: AgendaReminder | null; initialRequestId?: string; onClose: () => void; onSaved: () => void }
const localInput = (iso?: string) => iso ? new Date(iso).toISOString().slice(0, 16) : new Date(Date.now() + 3600000).toISOString().slice(0, 16)

export function AgendaReminderDialog({ open, condominiumId, reminder, initialRequestId, onClose, onSaved }: Props) {
  const [options, setOptions] = useState<{ units: Unit[]; requests: AgendaRequestOption[] }>({ units: [], requests: [] })
  const [title, setTitle] = useState(''); const [description, setDescription] = useState('')
  const [unitId, setUnitId] = useState(''); const [thirdParty, setThirdParty] = useState('')
  const [startsAt, setStartsAt] = useState(localInput()); const [recurrence, setRecurrence] = useState<AgendaRecurrence>('None')
  const [whatsApp, setWhatsApp] = useState(false); const [email, setEmail] = useState(false)
  const [requests, setRequests] = useState<AgendaRequestOption[]>([]); const [error, setError] = useState(''); const [saving, setSaving] = useState(false)
  useEffect(() => { if (!open) return; setTitle(reminder?.title ?? ''); setDescription(reminder?.description ?? ''); setUnitId(reminder?.unitId ?? ''); setThirdParty(reminder?.relatedThirdParty ?? ''); setStartsAt(localInput(reminder?.startsAtUtc)); setRecurrence(reminder?.recurrenceType ?? 'None'); setWhatsApp(reminder?.notifyByWhatsApp ?? false); setEmail(reminder?.notifyByEmail ?? false); setError(''); void getAgendaOptions(condominiumId, reminder?.id).then(data => { const units = data.units.map(u => ({ ...u, floor: null, description: null, isActive: true, createdAt: '', updatedAt: '' })) as Unit[]; setOptions({ units, requests: data.requests }); const ids = reminder?.requestIds ?? (initialRequestId ? [initialRequestId] : []); setRequests(data.requests.filter(r => ids.includes(r.id))) }).catch(() => setError('Não foi possível carregar as opções.')) }, [open, condominiumId, reminder, initialRequestId])
  const submit = async () => { setSaving(true); setError(''); try { const input: AgendaInput = { title, description: description || null, unitId: unitId || null, relatedThirdParty: thirdParty || null, startsAtUtc: new Date(startsAt).toISOString(), recurrenceType: recurrence, notifyByWhatsApp: whatsApp, notifyByEmail: email, requestIds: requests.map(r => r.id) }; await saveAgendaReminder(condominiumId, input, reminder?.id); onSaved() } catch { setError('Não foi possível salvar o lembrete. Revise os dados e tente novamente.') } finally { setSaving(false) } }
  return <Dialog open={open} onClose={onClose} fullWidth maxWidth="md"><DialogTitle>{reminder ? 'Editar lembrete' : 'Novo lembrete'}</DialogTitle><DialogContent><Stack spacing={2} pt={1}>
    <TextField label="Título" value={title} onChange={e => setTitle(e.target.value)} required inputProps={{ maxLength: 160 }} />
    <TextField label="Descrição curta" value={description} onChange={e => setDescription(e.target.value)} multiline minRows={2} inputProps={{ maxLength: 1000 }} />
    <UnitAutocomplete units={options.units} value={unitId} onChange={setUnitId} />
    <TextField label="Terceiro relacionado (opcional)" value={thirdParty} onChange={e => setThirdParty(e.target.value)} inputProps={{ maxLength: 200 }} />
    <Autocomplete multiple options={options.requests} value={requests} onChange={(_, value) => setRequests(value)} isOptionEqualToValue={(a, b) => a.id === b.id} getOptionLabel={r => `#${r.protocol.slice(0, 8)} · ${r.title} · ${r.residentName}${r.unitIdentifier ? ` · Apto ${r.unitIdentifier}` : ''} · ${r.status}`} renderInput={params => <TextField {...params} label="Atendimentos relacionados" />} />
    <Box display="grid" gridTemplateColumns={{ xs: '1fr', sm: '1fr 1fr' }} gap={2}><TextField type="datetime-local" label="Data e hora" value={startsAt} onChange={e => setStartsAt(e.target.value)} InputLabelProps={{ shrink: true }} /><TextField select label="Recorrência" value={recurrence} onChange={e => setRecurrence(e.target.value as AgendaRecurrence)}><MenuItem value="None">Não repetir</MenuItem><MenuItem value="Weekly">Toda semana</MenuItem><MenuItem value="Monthly">Todo mês</MenuItem></TextField></Box>
    <Box><FormControlLabel control={<Checkbox checked={whatsApp} onChange={e => setWhatsApp(e.target.checked)} />} label="Avisar por WhatsApp" /><FormControlLabel control={<Checkbox checked={email} onChange={e => setEmail(e.target.checked)} />} label="Avisar por e-mail" /></Box>
    {error && <Box color="error.main">{error}</Box>}
  </Stack></DialogContent><DialogActions><Button onClick={onClose}>Cancelar</Button><Button variant="contained" disabled={saving || !title.trim() || !startsAt} onClick={() => void submit()}>{saving ? 'Salvando…' : 'Salvar'}</Button></DialogActions></Dialog>
}
