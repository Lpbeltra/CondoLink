import { useEffect, useState } from 'react'
import { Alert, FormControlLabel, Paper, Stack, Switch, Typography } from '@mui/material'
import { getManagementCompanyEmployeeManagementModule, setManagementCompanyEmployeeManagementModule } from './api'

export function ManagementCompanyModules({ managementCompanyId }: { managementCompanyId: string }) {
  const [enabled, setEnabled] = useState(false)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  useEffect(() => {
    void getManagementCompanyEmployeeManagementModule(managementCompanyId)
      .then(module => setEnabled(module.isEnabled)).catch(() => setError('Não foi possível carregar os módulos.'))
      .finally(() => setLoading(false))
  }, [managementCompanyId])
  const toggle = async (value: boolean) => {
    setError('')
    try { await setManagementCompanyEmployeeManagementModule(managementCompanyId, value); setEnabled(value) }
    catch { setError('Não foi possível atualizar o módulo.') }
  }
  return <Paper variant="outlined" sx={{ p: 2 }}>
    <Stack gap={1}>
      <Typography variant="h3" fontSize={16} fontWeight={700}>Módulos</Typography>
      {error && <Alert severity="error">{error}</Alert>}
      <FormControlLabel control={<Switch checked={enabled} disabled={loading} onChange={event => void toggle(event.target.checked)} />}
        label="Gestão de Funcionários" />
    </Stack>
  </Paper>
}
