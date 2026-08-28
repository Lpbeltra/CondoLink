import { useCallback, useEffect, useState } from 'react'
import { Alert, Box, Checkbox, Chip, FormControlLabel, Paper, Skeleton, Stack, Switch, Typography } from '@mui/material'
import { EmptyState } from '../../components/EmptyState'
import { listManagementCompanyCategories, listManagementCompanyEmployees, setManagementCompanyAccessCategories, setManagementCompanyCategoryStatus } from './api'
import type { ManagementCompanyCategory, ManagementCompanyEmployee } from './types'

export function ManagementCompanyCategories({ managementCompanyId }: { managementCompanyId: string }) {
  const [categories, setCategories] = useState<ManagementCompanyCategory[]>([])
  const [accesses, setAccesses] = useState<ManagementCompanyEmployee[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const load = useCallback(async () => {
    setLoading(true); setError('')
    try {
      const [nextCategories, nextAccesses] = await Promise.all([listManagementCompanyCategories(managementCompanyId), listManagementCompanyEmployees(managementCompanyId)])
      setCategories(nextCategories); setAccesses(nextAccesses)
    } catch { setError('Não foi possível carregar as categorias e responsáveis.') }
    finally { setLoading(false) }
  }, [managementCompanyId])
  useEffect(() => { void load() }, [load])
  const toggleResponsible = async (category: ManagementCompanyCategory, access: ManagementCompanyEmployee) => {
    const next = new Set(access.categoryIds)
    if (next.has(category.id)) next.delete(category.id); else next.add(category.id)
    await setManagementCompanyAccessCategories(access.id, [...next])
    setAccesses(current => current.map(item => item.id === access.id ? { ...item, categoryIds: [...next] } : item))
  }
  if (loading) return <Skeleton variant="rounded" height={220} />
  if (error) return <Alert severity="error">{error}</Alert>
  if (!categories.length) return <EmptyState title="Nenhuma categoria" description="As categorias estruturais ainda não foram configuradas." />
  return <Stack gap={2}>
    <Box><Typography variant="h2">Categorias</Typography><Typography color="text.secondary">Ative categorias e defina um ou mais acessos responsáveis.</Typography></Box>
    {categories.map(category => {
      const responsible = accesses.filter(access => access.isActive && access.categoryIds.includes(category.id))
      return <Paper key={category.id} variant="outlined" sx={{ p: 2 }}>
        <Stack direction={{ xs: 'column', sm: 'row' }} justifyContent="space-between" gap={1}>
          <Box><Typography fontWeight={800}>{category.name}</Typography><Chip size="small" sx={{ mt: 1 }} color={responsible.length ? 'success' : 'warning'} label={responsible.length ? `${responsible.length} responsável(is)` : 'Indisponível: sem responsável'} /></Box>
          <FormControlLabel label={category.isActive ? 'Ativa' : 'Inativa'} control={<Switch checked={category.isActive} onChange={async (_, checked) => {
            await setManagementCompanyCategoryStatus(managementCompanyId, category.id, checked)
            setCategories(current => current.map(item => item.id === category.id ? { ...item, isActive: checked } : item))
          }} />} />
        </Stack>
        <Typography fontSize=".8rem" fontWeight={700} mt={2}>Responsáveis</Typography>
        <Stack direction="row" flexWrap="wrap" gap={1} mt={.5}>
          {accesses.length ? accesses.map(access => <FormControlLabel key={access.id} label={`${access.fullName}${access.isActive ? '' : ' (inativo)'}`}
            control={<Checkbox checked={access.categoryIds.includes(category.id)} disabled={!access.isActive} onChange={() => void toggleResponsible(category, access)} />} />)
            : <Typography color="text.secondary">Cadastre um acesso antes de atribuir responsáveis.</Typography>}
        </Stack>
      </Paper>
    })}
  </Stack>
}
