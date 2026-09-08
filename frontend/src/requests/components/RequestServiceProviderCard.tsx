import { useEffect, useState } from 'react'
import { Alert, Button, Card, CardContent, MenuItem, Stack, TextField, Typography } from '@mui/material'
import { listRequestServiceProviders, setRequestServiceProvider, type ServiceProviderOption } from '../api'
import type { ServiceProvider, ServiceProviderHistoryItem } from '../types'
import { ProviderWhatsAppDialog } from './ProviderWhatsAppDialog'

export function RequestServiceProviderCard({ requestId, status, current, history, onUpdated }: { requestId: string; status: string; current: ServiceProvider | null | undefined; history: ServiceProviderHistoryItem[]; onUpdated: () => void }) {
  const [options, setOptions] = useState<ServiceProviderOption[]>([])
  const [value, setValue] = useState(current?.id ?? '')
  const [error, setError] = useState('')
  const [contactOpen, setContactOpen] = useState(false)

  useEffect(() => {
    setValue(current?.id ?? '')
    void listRequestServiceProviders(requestId).then(setOptions).catch(() => setError('Não foi possível carregar prestadores.'))
  }, [requestId, current?.id])

  const save = async () => {
    try { await setRequestServiceProvider(requestId, value || null); onUpdated() }
    catch { setError('Não foi possível alterar o prestador.') }
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
        {current.phone
          ? <Button size="small" sx={{ mt: 1 }} onClick={() => setContactOpen(true)}>Contatar pelo WhatsApp</Button>
          : <Typography variant="caption" color="text.secondary">Prestador sem telefone cadastrado.</Typography>}
      </>}
      <Stack direction={{ xs: 'column', sm: 'row' }} gap={1.5} mt={2}>
        <TextField select label="Prestador" value={value} onChange={e => setValue(e.target.value)} fullWidth>
          <MenuItem value="">Sem prestador definido</MenuItem>
          {selectable.map(item => <MenuItem key={item.id} value={item.id}>{item.name} · {(item.specialties?.length ? item.specialties : [item.specialty]).join(', ')}{item.isMine ? ' · Meu prestador' : ''}</MenuItem>)}
        </TextField>
        <Button variant="contained" onClick={() => void save()}>Salvar</Button>
      </Stack>
      {error && <Alert severity="error" sx={{ mt: 1 }}>{error}</Alert>}
      {history.length > 0 && <Typography variant="caption" color="text.secondary" display="block" mt={1}>Histórico: {history.length} evento(s)</Typography>}
    </CardContent></Card>
    {contactOpen && current && <ProviderWhatsAppDialog requestId={requestId} provider={current} onClose={() => setContactOpen(false)} />}
  </>
}
