import { useCallback, useState } from 'react'
import { Alert, Button, CircularProgress, Stack, TextField, Typography } from '@mui/material'
import { useGuardedLoad } from '../../components/useGuardedLoad'
import { formatDateTime } from '../../requests/presentation'
import { api } from '../../services/api'
import { SuperlogicaCondominiumsDialog } from './SuperlogicaCondominiumsDialog'

type Integration = { provider: string; configured: boolean; status: string; lastValidatedAt: string | null }
const empty: Integration = { provider: 'Superlogica', configured: false, status: 'NotConfigured', lastValidatedAt: null }
const integrationLoadError = () => 'Não foi possível carregar a integração.'

export function ManagementCompanyIntegrations({ managementCompanyId }: { managementCompanyId: string }) {
  const path = `/overwatch/management-companies/${managementCompanyId}/integrations/superlogica`
  const load = useCallback(async () => (await api.get<Integration>(path)).data, [path])
  const { data, isLoading, setData } = useGuardedLoad(load, integrationLoadError)
  const [editing, setEditing] = useState(false)
  const [appToken, setAppToken] = useState('')
  const [accessToken, setAccessToken] = useState('')
  const [secret, setSecret] = useState('')
  const [working, setWorking] = useState(false)
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')

  const refresh = async () => setData((await api.get<Integration>(path)).data)
  const save = async () => {
    setWorking(true); setError(''); setMessage('')
    try {
      const response = await api.put<{ success: boolean; message: string; integration: Integration }>(path, { appToken, accessToken, secret })
      setData(response.data.integration); setMessage(response.data.message)
      setAppToken(''); setAccessToken(''); setSecret(''); setEditing(false)
    } catch { setError('Não foi possível salvar e validar as credenciais.') }
    finally { setWorking(false) }
  }
  const validate = async () => {
    setWorking(true); setError(''); setMessage('')
    try {
      const response = await api.post<{ success: boolean; message: string; integration: Integration }>(`${path}/validate`)
      setData(response.data.integration); setMessage(response.data.message)
    } catch { setError('Não foi possível validar a conexão.') }
    finally { setWorking(false) }
  }
  const disconnect = async () => {
    setWorking(true); setError('')
    try { await api.delete(path); setData(empty); setEditing(false); setMessage('Integração removida.') }
    catch { setError('Não foi possível remover a integração.') }
    finally { setWorking(false) }
  }

  if (isLoading || !data) return <CircularProgress size={24} />
  const statusLabel = data.status === 'Connected' ? 'Conectada' : data.status === 'Invalid' ? 'Credenciais inválidas' : data.status === 'ValidationFailed' ? 'Falha na última validação' : 'Não configurada'
  return <Stack spacing={2} maxWidth={620}>
    <Typography variant="h2">Superlógica</Typography>
    {error && <Alert severity="error">{error}</Alert>}
    {message && <Alert severity={data.status === 'Connected' ? 'success' : 'info'}>{message}</Alert>}
    {!editing && <>
      <Alert severity={data.status === 'Connected' ? 'success' : data.status === 'Invalid' ? 'error' : 'info'}>{statusLabel}</Alert>
      {data.configured && <Typography color="text.secondary">Última validação: {data.lastValidatedAt ? formatDateTime(data.lastValidatedAt) : 'Ainda não realizada'}</Typography>}
      {!data.configured && <Typography color="text.secondary">Conecte a Superlógica para permitir implantação e futuras funcionalidades integradas nos condomínios administrados.</Typography>}
      <Stack direction="row" gap={1} flexWrap="wrap">
        {data.configured && <SuperlogicaCondominiumsDialog managementCompanyId={managementCompanyId} />}
        {data.configured && <Button variant="outlined" onClick={() => void validate()} disabled={working}>Validar novamente</Button>}
        <Button variant={data.configured ? 'outlined' : 'contained'} onClick={() => { setError(''); setEditing(true) }}>{data.configured ? 'Atualizar credenciais' : 'Configurar integração'}</Button>
        {data.configured && <Button color="error" onClick={() => void disconnect()} disabled={working}>Desconectar</Button>}
      </Stack>
    </>}
    {editing && <>
      <TextField label="App Token" type="password" autoComplete="new-password" value={appToken} onChange={event => setAppToken(event.target.value)} />
      <TextField label="Access Token" type="password" autoComplete="new-password" value={accessToken} onChange={event => setAccessToken(event.target.value)} />
      <TextField label="Secret" type="password" autoComplete="new-password" value={secret} onChange={event => setSecret(event.target.value)} />
      <Stack direction="row" gap={1}>
        <Button variant="contained" onClick={() => void save()} disabled={working || !appToken || !accessToken || !secret}>{working ? <CircularProgress size={20} /> : 'Salvar e testar conexão'}</Button>
        <Button onClick={() => { setEditing(false); setAppToken(''); setAccessToken(''); setSecret('') }}>Cancelar</Button>
      </Stack>
    </>}
    <Button size="small" onClick={() => void refresh()} sx={{ alignSelf: 'flex-start' }}>Atualizar estado</Button>
  </Stack>
}
