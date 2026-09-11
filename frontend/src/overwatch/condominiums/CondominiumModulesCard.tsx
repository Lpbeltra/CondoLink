import { useEffect, useState } from 'react'
import { Alert, Card, CardContent, CircularProgress, FormControlLabel, Stack, Switch, Typography } from '@mui/material'
import { api } from '../../services/api'

interface ModuleState { module: string; enabled: boolean; managementCompanyAccessEnabled: boolean; supportsManagementCompanyAccess: boolean }
const labels: Record<string, string> = { Assistant: 'Assistente', Documents: 'Documentos', Providers: 'Prestadores', ManagementCompanyRequests: 'Solicitações para Administradora', EmployeeManagement: 'Gestão de Funcionários' }

export function CondominiumModulesCard({ condominiumId }: { condominiumId: string }) {
  const [items, setItems] = useState<ModuleState[]>([]); const [error, setError] = useState(''); const [saving, setSaving] = useState<string | null>(null)
  useEffect(() => { setItems([]); setError(''); void api.get<ModuleState[]>(`/overwatch/condominiums/${condominiumId}/modules`).then(x => setItems(x.data)).catch(() => setError('Não foi possível carregar os módulos.')) }, [condominiumId])
  const save = async (next: ModuleState, previous: ModuleState[]) => {
    setSaving(next.module); setItems(current => current.map(x => x.module === next.module ? next : x))
    try { await api.put(`/overwatch/condominiums/${condominiumId}/modules`, { modules: [{ module: next.module, enabled: next.enabled, managementCompanyAccessEnabled: next.managementCompanyAccessEnabled }] }) }
    catch { setItems(previous); setError('Não foi possível salvar o módulo.') } finally { setSaving(null) }
  }
  return <Card elevation={0} sx={{ mt: 3 }}><CardContent><Typography variant="h2">Módulos</Typography>{error && <Alert severity="error" sx={{ mt: 2 }}>{error}</Alert>}{items.length === 0 && !error ? <CircularProgress size={24} sx={{ mt: 2 }} /> : <Stack mt={1}>{items.map(item => <Stack key={item.module}><FormControlLabel control={<Switch checked={item.enabled} disabled={saving !== null} onChange={(_, enabled) => void save({ ...item, enabled, managementCompanyAccessEnabled: enabled ? item.managementCompanyAccessEnabled : false }, items)} />} label={labels[item.module] ?? item.module} />{item.supportsManagementCompanyAccess && <FormControlLabel sx={{ ml: 3 }} control={<Switch checked={item.managementCompanyAccessEnabled} disabled={!item.enabled || saving !== null} onChange={(_, managementCompanyAccessEnabled) => void save({ ...item, managementCompanyAccessEnabled }, items)} />} label="Permitir operação pela administradora" />}</Stack>)}</Stack>}</CardContent></Card>
}
