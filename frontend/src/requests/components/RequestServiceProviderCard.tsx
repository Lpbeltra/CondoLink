import { useEffect, useState } from 'react'
import { Alert, Button, Card, CardContent, CircularProgress, Dialog, DialogActions, DialogContent, DialogTitle, MenuItem, Stack, TextField, Typography } from '@mui/material'
import { listRequestServiceProviders, setRequestServiceProvider, type ServiceProviderOption } from '../api'
import type { ProviderPaymentRequestItem, ServiceProvider, ServiceProviderHistoryItem } from '../types'
import { ProviderWhatsAppDialog } from './ProviderWhatsAppDialog'
import { ProviderPaymentDialog } from './ProviderPaymentDialog'

export function RequestServiceProviderCard({ requestId, status, current, history, paymentRequests = [], requestTitle = 'Atendimento', condominium, unit, onUpdated }: { requestId: string; status: string; current: ServiceProvider | null | undefined; history: ServiceProviderHistoryItem[]; paymentRequests?: ProviderPaymentRequestItem[]; requestTitle?: string; condominium?: string; unit?: string | null; onUpdated: () => Promise<void> | void }) {
  const [options, setOptions] = useState<ServiceProviderOption[]>([])
  const [value, setValue] = useState(current?.id ?? '')
  const [error, setError] = useState('')
  const [contactOpen, setContactOpen] = useState(false)
  const [unlinkOpen, setUnlinkOpen] = useState(false)
  const [saving, setSaving] = useState(false)
  const [paymentOpen, setPaymentOpen] = useState(false)
  const [paymentFeedback, setPaymentFeedback] = useState('')

  useEffect(() => {
    setValue(current?.id ?? '')
    void listRequestServiceProviders(requestId).then(setOptions).catch(() => setError('Não foi possível carregar prestadores.'))
  }, [requestId, current?.id])

  const save = async () => {
    if (!current && !value) return
    setSaving(true); setError('')
    try { await setRequestServiceProvider(requestId, value || null); await onUpdated() }
    catch { setError('Não foi possível alterar o prestador.') }
    finally { setSaving(false) }
  }
  const unlink = async () => {
    setSaving(true); setError('')
    try { await setRequestServiceProvider(requestId, null); setUnlinkOpen(false); await onUpdated() }
    catch { setError('Não foi possível desvincular o prestador.') }
    finally { setSaving(false) }
  }
  const selectable = current && !options.some(item => item.id === current.id)
    ? [{ ...current, isMine: false, isCondominium: false }, ...options]
    : options

  if (!current && (status !== 'WaitingForThirdParty' || options.length === 0)) return null

  return <>
    <Card elevation={0} sx={{ mt: 3 }}><CardContent>
      <Typography variant="h2">Prestador</Typography>
      {current && <>
        <Typography color="text.secondary" mt={.5}>
          {current.name}{current.companyName ? ` · ${current.companyName}` : ''} · {(current.specialties?.length ? current.specialties : [current.specialty]).join(', ')} · {current.phone}{!current.isActive ? ' · Inativo' : ''}
        </Typography>
        <Stack direction="row" flexWrap="wrap" gap={1} mt={1}>
          {current.phone && <Button size="small" onClick={() => setContactOpen(true)}>Contatar pelo WhatsApp</Button>}
          <Button size="small" variant="contained" disabled={saving} onClick={() => setPaymentOpen(true)}>Solicitar pagamento</Button>
          <Button size="small" color="error" disabled={saving} onClick={() => setUnlinkOpen(true)}>Desvincular prestador</Button>
        </Stack>
        {!current.phone && <Typography variant="caption" color="text.secondary">Prestador sem telefone cadastrado.</Typography>}
      </>}
      <Stack direction={{ xs: 'column', sm: 'row' }} gap={1.5} mt={2}>
        <TextField select label="Prestador" value={value} onChange={e => setValue(e.target.value)} disabled={saving} fullWidth>
          <MenuItem value="">Sem prestador definido</MenuItem>
          {selectable.map(item => <MenuItem key={item.id} value={item.id}>{item.name} · {(item.specialties?.length ? item.specialties : [item.specialty]).join(', ')}{item.isMine ? ' · Meu prestador' : ''}</MenuItem>)}
        </TextField>
        <Button variant="contained" disabled={saving || (!current && !value)} onClick={() => void save()}>{saving ? <CircularProgress size={20} color="inherit" /> : 'Salvar'}</Button>
      </Stack>
      {error && <Alert severity="error" sx={{ mt: 1 }}>{error}</Alert>}
      {paymentFeedback && <Alert severity="success" sx={{ mt: 1 }}>Pagamento solicitado à administradora ({paymentFeedback}).</Alert>}
      {paymentRequests.length > 0 && <Typography variant="caption" color="text.secondary" display="block" mt={1}>Pagamento solicitado à administradora · {paymentRequests.length} solicitação(ões) relacionada(s).</Typography>}
      {history.length > 0 && <Typography variant="caption" color="text.secondary" display="block" mt={1}>Histórico: {history.length} evento(s)</Typography>}
    </CardContent></Card>
    {contactOpen && current && <ProviderWhatsAppDialog requestId={requestId} provider={current} onClose={() => setContactOpen(false)} />}
    {paymentOpen && current && <ProviderPaymentDialog requestId={requestId} provider={current} requestTitle={requestTitle} condominium={condominium} unit={unit} onClose={() => setPaymentOpen(false)} onCreated={async identifier => { setPaymentFeedback(identifier); await onUpdated() }} />}
    <Dialog open={unlinkOpen} onClose={saving ? undefined : () => setUnlinkOpen(false)} fullWidth maxWidth="xs">
      <DialogTitle>Desvincular prestador?</DialogTitle>
      <DialogContent><Typography>Desvincular {current?.name} deste atendimento?</Typography><Typography color="text.secondary" mt={1}>O prestador continuará cadastrado e o histórico deste atendimento será preservado.</Typography></DialogContent>
      <DialogActions><Button disabled={saving} onClick={() => setUnlinkOpen(false)}>Cancelar</Button><Button color="error" variant="contained" disabled={saving} onClick={() => void unlink()}>{saving ? <CircularProgress size={20} color="inherit" /> : 'Desvincular'}</Button></DialogActions>
    </Dialog>
  </>
}
