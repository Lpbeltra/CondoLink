import { useCallback, useEffect, useState } from 'react'
import FilterAltOffRoundedIcon from '@mui/icons-material/FilterAltOffRounded'
import { Alert, Box, Button, Card, CardContent, FormControl, Grid, InputLabel, MenuItem, Select, Skeleton, Stack, Typography } from '@mui/material'
import { EmptyState } from '../components/EmptyState'
import { PageContainer } from '../components/PageContainer'
import { useCondominium } from '../condominiums/CondominiumContext'
import { listManagementRequests } from '../requests/api'
import { ManagementRequestCard } from '../requests/components/ManagementRequestCard'
import { getRequestError, priorityPresentation, statusPresentation } from '../requests/presentation'
import type { ManagementRequestsResponse, RequestPriority, RequestStatus } from '../requests/types'

const summaries = [
  ['Abertas', 'open'], ['Em andamento', 'inProgress'], ['Aguardando morador', 'waitingForResident'], ['Aguardando terceiro', 'waitingForThirdParty'], ['Resolvidas', 'resolved'], ['Canceladas', 'cancelled'],
] as const

export function ManagementRequestsPage() {
  const { currentCondominium, isManager } = useCondominium()
  const [status, setStatus] = useState<RequestStatus | ''>('')
  const [priority, setPriority] = useState<RequestPriority | ''>('')
  const [data, setData] = useState<ManagementRequestsResponse | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState('')

  const load = useCallback(async () => {
    if (!currentCondominium || !isManager) return
    setIsLoading(true); setError(''); setData(null)
    try { setData(await listManagementRequests(currentCondominium.condominium.id, { status: status || undefined, priority: priority || undefined })) }
    catch (requestError) { setError(getRequestError(requestError)) }
    finally { setIsLoading(false) }
  }, [currentCondominium, isManager, priority, status])

  useEffect(() => { void load() }, [load])
  const clearFilters = () => { setStatus(''); setPriority('') }

  if (!isManager) return <PageContainer><EmptyState title="Acesso não disponível" description="O atendimento está disponível somente para gestores deste condomínio." /></PageContainer>

  return <PageContainer>
    <Typography variant="h1">Atendimento</Typography><Typography color="text.secondary" mt={.5}>Acompanhe e organize as solicitações do condomínio.</Typography>
    {isLoading ? <Skeleton variant="rounded" height={120} sx={{ mt: 3 }} /> : data && <Box display="flex" gap={1.5} mt={3} pb={1} sx={{ overflowX: 'auto', scrollbarWidth: 'thin' }}>{summaries.map(([label, key]) => <Card key={key} elevation={0} sx={{ minWidth: { xs: 138, md: 0 }, flex: { md: 1 }, boxShadow: 'none' }}><CardContent sx={{ p: 2, '&:last-child': { pb: 2 } }}><Typography color="text.secondary" fontSize=".75rem" fontWeight={700}>{label}</Typography><Typography variant="h2" mt={.5}>{data.counts[key]}</Typography></CardContent></Card>)}</Box>}
    <Stack direction={{ xs: 'column', sm: 'row' }} gap={1.5} my={3} alignItems={{ sm: 'center' }}>
      <FormControl size="small" sx={{ minWidth: { sm: 210 } }}><InputLabel>Status</InputLabel><Select label="Status" value={status} onChange={(event) => setStatus(event.target.value as RequestStatus | '')}><MenuItem value="">Todos</MenuItem>{Object.entries(statusPresentation).map(([value, item]) => <MenuItem key={value} value={value}>{item.label}</MenuItem>)}</Select></FormControl>
      <FormControl size="small" sx={{ minWidth: { sm: 180 } }}><InputLabel>Prioridade</InputLabel><Select label="Prioridade" value={priority} onChange={(event) => setPriority(event.target.value as RequestPriority | '')}><MenuItem value="">Todas</MenuItem>{Object.entries(priorityPresentation).map(([value, item]) => <MenuItem key={value} value={value}>{item.label}</MenuItem>)}</Select></FormControl>
      <Button startIcon={<FilterAltOffRoundedIcon />} onClick={clearFilters} disabled={!status && !priority}>Limpar filtros</Button>
    </Stack>
    {error && <Alert severity="error" action={<Button color="inherit" onClick={() => void load()}>Tentar novamente</Button>}>{error}</Alert>}
    {isLoading ? <Grid container spacing={2}>{[1, 2].map((item) => <Grid key={item} size={{ xs: 12, lg: 6 }}><Skeleton variant="rounded" height={170} /></Grid>)}</Grid> : data && data.items.length === 0 ? <EmptyState title="Nenhuma solicitação encontrada" description="Não há solicitações correspondentes aos filtros atuais." action={<Button variant="contained" onClick={clearFilters}>Limpar filtros</Button>} /> : data && <Grid container spacing={2}>{data.items.map((request) => <Grid key={request.id} size={{ xs: 12, lg: 6 }}><ManagementRequestCard request={request} /></Grid>)}</Grid>}
  </PageContainer>
}
