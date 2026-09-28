import { useCallback, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  Alert, Button, CircularProgress, Dialog, DialogActions, DialogContent,
  DialogTitle, MenuItem, Stack, TextField, Typography,
} from '@mui/material'
import { useGuardedLoad } from '../../components/useGuardedLoad'
import { api } from '../../services/api'
import { formatCnpj } from '../registration'

type Candidate = { id: string; name: string; cnpj: string | null }
type ExternalCondominium = {
  externalId: string; name: string; tradeName: string | null; taxId: string | null
  address: string | null; addressComplement: string | null; neighborhood: string | null
  city: string | null; state: string | null; zipCode: string | null
}
type DiscoveryItem = {
  condominium: ExternalCondominium
  mapping: { id: string; condominiumId: string; name: string } | null
  cnpjCandidate: Candidate | null
}
type Discovery = { administratorName: string; total: number; availableCondominiums: Candidate[]; items: DiscoveryItem[] }

export function SuperlogicaCondominiumsDialog({ managementCompanyId }: { managementCompanyId: string }) {
  const navigate = useNavigate()
  const path = `/overwatch/management-companies/${managementCompanyId}/integrations/superlogica`
  const [open, setOpen] = useState(false)
  const [reload, setReload] = useState(0)
  const load = useCallback(async () => {
    void reload
    return open ? (await api.get<Discovery>(`${path}/condominiums`)).data : null as unknown as Discovery
  }, [path, reload, open])
  const toDiscoveryError = useCallback((e: unknown) => {
    const response = (e as { response?: { data?: { code?: string } } }).response
    return response?.data?.code === 'invalid_credentials'
      ? 'Credenciais inválidas. Revise a integração da Superlógica.'
      : 'Não foi possível consultar a Superlógica no momento. Tente novamente mais tarde.'
  }, [])
  const { data, isLoading, error } = useGuardedLoad(load, toDiscoveryError)
  const [selected, setSelected] = useState<DiscoveryItem | null>(null)
  const [creating, setCreating] = useState(false)
  const [confirmUnlink, setConfirmUnlink] = useState<DiscoveryItem | null>(null)
  const [search, setSearch] = useState('')
  const [condominiumId, setCondominiumId] = useState('')
  const [busy, setBusy] = useState(false)
  const [operationError, setOperationError] = useState('')
  const [form, setForm] = useState({ name: '', cnpj: '', address: '', city: '', state: '' })

  const refresh = () => setReload((value) => value + 1)
  const openLink = (item: DiscoveryItem, candidate?: Candidate | null) => {
    setSelected(item); setCreating(false); setCondominiumId(candidate?.id ?? ''); setSearch(''); setOperationError('')
  }
  const openCreate = (item: DiscoveryItem) => {
    setSelected(item); setCreating(true); setOperationError('')
    const x = item.condominium
    setForm({ name: x.name, cnpj: x.taxId ?? '', address: x.address ?? '', city: x.city ?? '', state: x.state ?? '' })
  }
  const link = async () => {
    if (!selected || !condominiumId) return
    setBusy(true); setOperationError('')
    try {
      await api.post(`${path}/mappings`, { externalCondominiumId: selected.condominium.externalId, condominiumId })
      setSelected(null); refresh()
    } catch { setOperationError('Não foi possível vincular. Verifique se condomínio ainda pertence à administradora e tente novamente.') }
    finally { setBusy(false) }
  }
  const unlink = async () => {
    if (!confirmUnlink?.mapping) return
    setBusy(true); setOperationError('')
    try {
      await api.delete(`${path}/mappings/${confirmUnlink.mapping.id}`)
      setConfirmUnlink(null); refresh()
    } catch { setOperationError('Não foi possível desvincular da Superlógica.') }
    finally { setBusy(false) }
  }
  const create = async () => {
    if (!selected) return
    setBusy(true); setOperationError('')
    try {
      await api.post(`${path}/condominiums/create`, { externalCondominiumId: selected.condominium.externalId, ...form })
      setSelected(null); refresh()
    } catch (e) {
      const response = (e as { response?: { data?: { message?: string } } }).response
      setOperationError(response?.data?.message ?? 'Não foi possível criar e vincular condomínio.')
    } finally { setBusy(false) }
  }

  return <>
    <Button variant="outlined" onClick={() => setOpen(true)}>Ver condomínios</Button>
    <Dialog open={open} onClose={() => !busy && setOpen(false)} fullWidth maxWidth="md">
      <DialogTitle>Condomínios da Superlógica</DialogTitle>
      <DialogContent>
        {isLoading && <CircularProgress size={24} />}
        {error && <Alert severity="error" action={<Button onClick={refresh}>Tentar novamente</Button>}>{error}</Alert>}
        {operationError && <Alert severity="error" sx={{ my: 1 }}>{operationError}</Alert>}
        {data && <>
          <Typography variant="h2">SUPERLÓGICA — {data.administratorName}</Typography>
          <Typography color="text.secondary" sx={{ mb: 2 }}>{data.total} {data.total === 1 ? 'condomínio encontrado' : 'condomínios encontrados'}</Typography>
          {data.items.length === 0 && <Typography color="text.secondary">Nenhum condomínio retornado pela Superlógica.</Typography>}
          <Stack divider={<div style={{ borderBottom: '1px solid #ddd' }} />} spacing={1.5}>
            {data.items.map((item) => {
              const external = item.condominium
              return <Stack key={external.externalId} direction={{ xs: 'column', sm: 'row' }} justifyContent="space-between" gap={1.5} py={1}>
                <Stack spacing={0.25}>
                  <Typography fontWeight={700}>{external.name}</Typography>
                  <Typography color="text.secondary">{[external.city, external.state].filter(Boolean).join('/') || 'Localização não informada'}</Typography>
                  {external.taxId && <Typography color="text.secondary">CNPJ: {formatCnpj(external.taxId)}</Typography>}
                  <Typography variant="caption" color="text.secondary">Superlógica #{external.externalId}</Typography>
                  {item.mapping && <Button size="small" color="success" sx={{ alignSelf: 'flex-start', px: 0 }} onClick={() => navigate(`/overwatch/condominiums/${item.mapping!.condominiumId}`)}>Vinculado ao Comvy: {item.mapping.name}</Button>}
                  {!item.mapping && <Typography color="text.secondary">Não vinculado</Typography>}
                  {!item.mapping && item.cnpjCandidate && <Typography color="primary.main">Encontramos possível correspondência: {item.cnpjCandidate.name}</Typography>}
                </Stack>
                {item.mapping
                  ? <Button color="error" onClick={() => setConfirmUnlink(item)}>Desvincular da Superlógica</Button>
                  : <Stack direction="row" gap={1} flexWrap="wrap" alignItems="flex-start">
                    {item.cnpjCandidate && <Button onClick={() => openLink(item, item.cnpjCandidate)}>Vincular existente</Button>}
                    <Button onClick={() => openLink(item)}>Escolher outro</Button>
                    <Button onClick={() => openCreate(item)}>Criar no Comvy</Button>
                  </Stack>}
              </Stack>
            })}
          </Stack>
        </>}
      </DialogContent>
      <DialogActions><Button onClick={() => setOpen(false)}>Fechar</Button></DialogActions>
    </Dialog>

    <Dialog open={!!selected} onClose={() => !busy && setSelected(null)} fullWidth maxWidth="sm">
      <DialogTitle>{selected && (creating ? `Criar no Comvy: ${selected.condominium.name}` : `Vincular ${selected.condominium.name}`)}</DialogTitle>
      <DialogContent>
        {selected && !creating && <Stack spacing={2} sx={{ pt: 1 }}>
          <Typography>Superlógica: {selected.condominium.name} — #{selected.condominium.externalId}</Typography>
          {selected.cnpjCandidate && <Typography>Possível correspondência Comvy: {selected.cnpjCandidate.name}</Typography>}
          <TextField label="Buscar condomínio da administradora" value={search} onChange={(e) => setSearch(e.target.value)} />
          <TextField select label="Condomínio Comvy" value={condominiumId} onChange={(e) => setCondominiumId(e.target.value)}>
            {data?.availableCondominiums.filter((x) => x.name.toLocaleLowerCase().includes(search.toLocaleLowerCase())).map((x) => <MenuItem key={x.id} value={x.id}>{x.name}</MenuItem>)}
          </TextField>
        </Stack>}
        {selected && creating && <Stack spacing={2} sx={{ pt: 1 }}>
          <Typography>Revise os dados. Administradora: {data?.administratorName}</Typography>
          {selected.condominium.tradeName && <Typography>Nome fantasia: {selected.condominium.tradeName}</Typography>}
          {(['name', 'cnpj', 'address', 'city', 'state'] as const).map((field) => <TextField key={field} label={{ name: 'Nome', cnpj: 'CNPJ', address: 'Endereço', city: 'Cidade', state: 'UF' }[field]} value={form[field]} onChange={(e) => setForm({ ...form, [field]: e.target.value })} />)}
          <Typography variant="caption" color="text.secondary">Complemento, bairro e CEP recebidos: {selected.condominium.addressComplement || '—'}, {selected.condominium.neighborhood || '—'}, {selected.condominium.zipCode || '—'}. O modelo atual do Comvy não armazena esses campos.</Typography>
        </Stack>}
      </DialogContent>
      <DialogActions><Button onClick={() => setSelected(null)}>Cancelar</Button><Button variant="contained" disabled={busy || (creating ? !form.name || !form.cnpj || !form.address || !form.city || !form.state : !condominiumId)} onClick={() => void (creating ? create() : link())}>{creating ? 'Criar e vincular' : 'Vincular condomínios'}</Button></DialogActions>
    </Dialog>

    <Dialog open={!!confirmUnlink} onClose={() => setConfirmUnlink(null)}>
      <DialogTitle>Desvincular da Superlógica?</DialogTitle>
      <DialogContent><Typography>Somente o vínculo será removido. O condomínio Comvy e seus dados permanecerão.</Typography></DialogContent>
      <DialogActions><Button onClick={() => setConfirmUnlink(null)}>Cancelar</Button><Button color="error" onClick={() => void unlink()} disabled={busy}>Desvincular da Superlógica</Button></DialogActions>
    </Dialog>
  </>
}
