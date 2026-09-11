import { useEffect, useState } from 'react'
import { Alert, MenuItem, Stack, TextField, Typography } from '@mui/material'
import { PageContainer } from '../../components/PageContainer'
import { EmployeesManager } from '../../employees/EmployeesManager'
import { listDelegatedCondominiums, type DelegatedCondominium } from './api'

export function AdministratorEmployeeManagementPage() {
  const [condominiums, setCondominiums] = useState<DelegatedCondominium[]>([])
  const [condominiumId, setCondominiumId] = useState('')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    listDelegatedCondominiums()
      .then(list => {
        setCondominiums(list)
        if (list.length === 1) setCondominiumId(list[0].condominiumId)
      })
      .catch(() => setError('Não foi possível carregar os condomínios.'))
      .finally(() => setLoading(false))
  }, [])

  if (loading) return <PageContainer><Alert severity="info">Carregando…</Alert></PageContainer>
  if (error) return <PageContainer><Alert severity="error">{error}</Alert></PageContainer>
  if (condominiums.length === 0)
    return <PageContainer><Alert severity="info">Nenhum condomínio delegou a Gestão de Funcionários para sua administradora.</Alert></PageContainer>

  return (
    <PageContainer maxWidth={1200}>
      <Stack gap={2}>
        <div><Typography variant="h1">Funcionários</Typography></div>
        <TextField select label="Condomínio" value={condominiumId} onChange={e => setCondominiumId(e.target.value)} sx={{ maxWidth: 360 }}>
          <MenuItem value="">Selecione</MenuItem>
          {condominiums.map(c => <MenuItem key={c.condominiumId} value={c.condominiumId}>{c.name}</MenuItem>)}
        </TextField>
        {condominiumId && <EmployeesManager condominiumId={condominiumId} />}
      </Stack>
    </PageContainer>
  )
}
