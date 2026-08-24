import { useEffect, useMemo, useState } from 'react'
import { Alert, Box, Button, Checkbox, Dialog, DialogActions, DialogContent, DialogTitle, FormControlLabel, MenuItem, Paper, Stack, TextField, Typography } from '@mui/material'
import { UnitAutocomplete, blockLabel } from '../management/components/UnitAutocomplete'
import type { Unit } from '../management/types'
import { statusPresentation } from '../requests/presentation'
import type { RequestStatus } from '../requests/types'
import { getAgendaOptions, saveAgendaReminder } from './api'
import type { AgendaInput, AgendaReminder, AgendaRequestOption, AgendaRecurrence } from './types'

interface Props { open: boolean; condominiumId: string; reminder?: AgendaReminder | null; initialRequestId?: string; onClose: () => void; onSaved: () => void }
const localInput = (iso?: string) => iso ? new Date(iso).toISOString().slice(0, 16) : new Date(Date.now() + 3600000).toISOString().slice(0, 16)
const normalize = (value: string) => value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase()

export function filterAgendaRequests(requests: AgendaRequestOption[], search: string) {
  const term = normalize(search.trim())
  if (!term) return requests
  return requests.filter(request => normalize([request.protocol, request.title, request.residentName, request.block, request.unitIdentifier].filter(Boolean).join(' ')).includes(term))
}

export function toggleAgendaRequest(ids: string[], requestId: string, fixedId?: string) {
  if (requestId === fixedId) return ids.includes(requestId) ? ids : [...ids, requestId]
  return ids.includes(requestId) ? ids.filter(id => id !== requestId) : [...ids, requestId]
}

export function AgendaReminderDialog({ open, condominiumId, reminder, initialRequestId, onClose, onSaved }: Props) {
  const [units, setUnits] = useState<Unit[]>([]), [requestOptions, setRequestOptions] = useState<AgendaRequestOption[]>([])
  const [selectedRequestIds, setSelectedRequestIds] = useState<string[]>([]), [requestSearch, setRequestSearch] = useState('')
  const [title, setTitle] = useState(''), [description, setDescription] = useState(''), [unitId, setUnitId] = useState(''), [thirdParty, setThirdParty] = useState('')
  const [startsAt, setStartsAt] = useState(localInput()), [recurrence, setRecurrence] = useState<AgendaRecurrence>('None')
  const [whatsApp, setWhatsApp] = useState(false), [email, setEmail] = useState(false), [error, setError] = useState(''), [loading, setLoading] = useState(false), [saving, setSaving] = useState(false)
  useEffect(() => {
    if (!open) return
    setTitle(reminder?.title ?? ''); setDescription(reminder?.description ?? ''); setUnitId(reminder?.unitId ?? ''); setThirdParty(reminder?.relatedThirdParty ?? '')
    setStartsAt(localInput(reminder?.startsAtUtc)); setRecurrence(reminder?.recurrenceType ?? 'None'); setWhatsApp(reminder?.notifyByWhatsApp ?? false); setEmail(reminder?.notifyByEmail ?? false); setRequestSearch(''); setError(''); setLoading(true)
    void getAgendaOptions(condominiumId, reminder?.id).then(data => {
      setUnits(data.units.map(unit => ({ ...unit, floor: null, description: null, isActive: true, createdAt: '', updatedAt: '' })))
      setRequestOptions(data.requests)
      const ids = reminder?.requestIds ?? []
      setSelectedRequestIds(initialRequestId && !ids.includes(initialRequestId) ? [...ids, initialRequestId] : ids)
    }).catch(() => setError('Não foi possível carregar unidades e atendimentos.')).finally(() => setLoading(false))
  }, [open, condominiumId, reminder, initialRequestId])
  const visibleRequests = useMemo(() => filterAgendaRequests(requestOptions, requestSearch), [requestOptions, requestSearch])
  const origin = initialRequestId ? requestOptions.find(item => item.id === initialRequestId) : undefined
  const submit = async () => {
    setSaving(true); setError('')
    try {
      const input: AgendaInput = { title, description: description || null, unitId: unitId || null, relatedThirdParty: thirdParty || null, startsAtUtc: new Date(startsAt).toISOString(), recurrenceType: recurrence, notifyByWhatsApp: whatsApp, notifyByEmail: email, requestIds: selectedRequestIds }
      await saveAgendaReminder(condominiumId, input, reminder?.id); onSaved()
    } catch { setError('Não foi possível salvar o lembrete. Revise os dados e tente novamente.') } finally { setSaving(false) }
  }
  return <Dialog open={open} onClose={onClose} fullWidth maxWidth="md"><DialogTitle>{reminder ? 'Editar lembrete' : 'Novo lembrete'}</DialogTitle><DialogContent><Stack spacing={2} pt={1}>
    <TextField label="Título" value={title} onChange={event => setTitle(event.target.value)} required inputProps={{ maxLength: 160 }} />
    <TextField label="Descrição curta" value={description} onChange={event => setDescription(event.target.value)} multiline minRows={2} inputProps={{ maxLength: 1000 }} />
    <UnitAutocomplete units={units} value={unitId} onChange={setUnitId} disabled={loading} />
    <TextField label="Terceiro relacionado (opcional)" value={thirdParty} onChange={event => setThirdParty(event.target.value)} inputProps={{ maxLength: 200 }} />
    <Box><Typography variant="h3" mb={1}>Atendimentos relacionados</Typography>
      {origin && <Alert severity="info" sx={{ mb: 1 }}>Atendimento de origem: <strong>#{origin.protocol} · {origin.title}</strong>. Ele será mantido neste lembrete.</Alert>}
      <TextField fullWidth size="small" label="Buscar por protocolo, título, morador ou unidade" value={requestSearch} onChange={event => setRequestSearch(event.target.value)} />
      <Paper variant="outlined" sx={{ mt: 1, maxHeight: 300, overflowY: 'auto' }}>
        {loading ? <Typography p={2}>Carregando…</Typography> : visibleRequests.length === 0 ? <Typography p={2} color="text.secondary">Nenhum atendimento elegível.</Typography> : visibleRequests.map(request => {
          const location = [request.block && blockLabel(request.block), request.unitIdentifier && `Apto ${request.unitIdentifier}`].filter(Boolean).join(' · ')
          return <Box key={request.id} sx={{ px: 1.5, py: .75, borderBottom: '1px solid', borderColor: 'divider', '&:last-child': { borderBottom: 0 } }}><FormControlLabel sx={{ m: 0, alignItems: 'flex-start' }} control={<Checkbox checked={selectedRequestIds.includes(request.id)} disabled={request.id === initialRequestId} onChange={() => setSelectedRequestIds(current => toggleAgendaRequest(current, request.id, initialRequestId))} />} label={<Box pt={.65}><Typography fontWeight={750}>#{request.protocol} · {request.title}</Typography><Typography variant="body2" color="text.secondary">{[request.residentName, location, statusPresentation[request.status as RequestStatus]?.label ?? request.status].filter(Boolean).join(' · ')}</Typography></Box>} /></Box>
        })}
      </Paper>
    </Box>
    <Box display="grid" gridTemplateColumns={{ xs: '1fr', sm: '1fr 1fr' }} gap={2}><TextField type="datetime-local" label="Data e hora" value={startsAt} onChange={event => setStartsAt(event.target.value)} InputLabelProps={{ shrink: true }} /><TextField select label="Recorrência" value={recurrence} onChange={event => setRecurrence(event.target.value as AgendaRecurrence)}><MenuItem value="None">Não repetir</MenuItem><MenuItem value="Weekly">Toda semana</MenuItem><MenuItem value="Monthly">Todo mês</MenuItem></TextField></Box>
    <Box><FormControlLabel control={<Checkbox checked={whatsApp} onChange={event => setWhatsApp(event.target.checked)} />} label="Avisar por WhatsApp" /><FormControlLabel control={<Checkbox checked={email} onChange={event => setEmail(event.target.checked)} />} label="Avisar por e-mail" /></Box>
    {error && <Alert severity="error">{error}</Alert>}
  </Stack></DialogContent><DialogActions><Button onClick={onClose}>Cancelar</Button><Button variant="contained" disabled={saving || loading || !title.trim() || !startsAt} onClick={() => void submit()}>{saving ? 'Salvando…' : 'Salvar'}</Button></DialogActions></Dialog>
}
