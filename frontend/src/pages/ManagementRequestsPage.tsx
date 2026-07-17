import { useCallback, useEffect, useState } from 'react'
import FilterAltOffRoundedIcon from '@mui/icons-material/FilterAltOffRounded'
import SearchRoundedIcon from '@mui/icons-material/SearchRounded'
import { Alert, Box, Button, Card, CardActionArea, CardContent, FormControl, Grid, InputAdornment, InputLabel, MenuItem, Select, Skeleton, Stack, TextField, Typography } from '@mui/material'
import { useSearchParams } from 'react-router-dom'
import { EmptyState } from '../components/EmptyState'
import { PageContainer } from '../components/PageContainer'
import { useCondominium } from '../condominiums/CondominiumContext'
import { listManagementRequests } from '../requests/api'
import { ManagementRequestCard } from '../requests/components/ManagementRequestCard'
import { applySummaryFilter, selectManagementRequests } from '../requests/managementRequests'
import { getRequestError, priorityPresentation, statusPresentation } from '../requests/presentation'
import type { ManagementRequestsResponse, RequestPriority, RequestStatus } from '../requests/types'
import { listCategories } from '../management/api'
import type { Category } from '../management/types'

const summaries = [
  ['Abertas', 'open', 'Open'], ['Em andamento', 'inProgress', 'InProgress'], ['Aguardando morador', 'waitingForResident', 'WaitingForResident'], ['Aguardando terceiro', 'waitingForThirdParty', 'WaitingForThirdParty'], ['Resolvidas', 'resolved', 'Resolved'], ['Canceladas', 'cancelled', 'Cancelled'],
] as const

export function ManagementRequestsPage() {
  const { currentCondominium, isManager } = useCondominium()
  const [searchParams,setSearchParams] = useSearchParams()
  const [categories,setCategories] = useState<Category[]>([])
  const requestedCategoryId = searchParams.get('categoryId') ?? ''
  const categoryId = categories.some(category=>category.id===requestedCategoryId) ? requestedCategoryId : ''
  const [status, setStatus] = useState<RequestStatus | ''>('')
  const [priority, setPriority] = useState<RequestPriority | ''>('')
  const [search, setSearch] = useState('')
  const [data, setData] = useState<ManagementRequestsResponse | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState('')

  const load = useCallback(async () => {
    if (!currentCondominium || !isManager) return
    setIsLoading(true); setError(''); setData(null)
    try { setData(await listManagementRequests(currentCondominium.condominium.id, { status: status || undefined, priority: priority || undefined, categoryId: categoryId || undefined })) }
    catch (requestError) { setError(getRequestError(requestError)) }
    finally { setIsLoading(false) }
  }, [categoryId, currentCondominium, isManager, priority, status])

  useEffect(() => { void load() }, [load])
  useEffect(()=>{const id=currentCondominium?.condominium.id;if(!id||!isManager)return;void listCategories(id).then(items=>{setCategories(items);const requested=searchParams.get('categoryId');if(requested&&!items.some(item=>item.id===requested)){const next=new URLSearchParams(searchParams);next.delete('categoryId');setSearchParams(next,{replace:true})}})},[currentCondominium?.condominium.id,isManager,searchParams,setSearchParams])
  const setCategory=(value:string)=>{const next=new URLSearchParams(searchParams);if(value)next.set('categoryId',value);else next.delete('categoryId');setSearchParams(next)}
  const clearFilters = () => { setStatus(''); setPriority(''); setSearch(''); setCategory('') }
  const selectSummary = (selectedStatus: RequestStatus) => {
    const filters = applySummaryFilter(selectedStatus, search)
    setStatus(filters.status); setPriority(filters.priority)
  }
  const visibleItems = data ? selectManagementRequests(data.items, status, search) : []

  if (!isManager) return <PageContainer><EmptyState title="Acesso não disponível" description="O atendimento está disponível somente para gestores deste condomínio." /></PageContainer>

  return <PageContainer>
    <Typography variant="h1">Atendimento</Typography><Typography color="text.secondary" mt={.5}>Acompanhe e organize as solicitações do condomínio.</Typography>
    {isLoading ? <Skeleton variant="rounded" height={120} sx={{ mt: 3 }} /> : data && <Box display="flex" gap={1.5} mt={3} pb={1} sx={{ overflowX: 'auto', scrollbarWidth: 'thin' }}>{summaries.map(([label, key, summaryStatus]) => <Card key={key} elevation={0} sx={{ minWidth: { xs: 150, md: 0 }, flex: { md: 1 }, boxShadow: 'none', border: '1px solid', borderColor: status === summaryStatus ? 'primary.main' : 'divider', bgcolor: status === summaryStatus ? 'rgba(31,94,255,.045)' : 'background.paper' }}><CardActionArea onClick={() => selectSummary(summaryStatus)} aria-label={`Filtrar por ${label}`} sx={{ height: '100%', borderRadius: 'inherit', '&:focus-visible': { outline: '3px solid', outlineColor: 'primary.light', outlineOffset: -3 } }}><CardContent sx={{ p: 2, '&:last-child': { pb: 2 } }}><Typography color="text.secondary" fontSize=".75rem" fontWeight={700}>{label}</Typography><Typography variant="h2" mt={.5}>{data.counts[key]}</Typography></CardContent></CardActionArea></Card>)}</Box>}
    <Stack direction={{ xs: 'column', sm: 'row' }} gap={1.5} my={3} alignItems={{ sm: 'center' }}>
      <TextField size="small" label="Buscar" value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Título, morador, categoria ou unidade" sx={{ minWidth: { sm: 260 }, flex: 1 }} InputProps={{ startAdornment: <InputAdornment position="start"><SearchRoundedIcon /></InputAdornment> }} />
      <FormControl size="small" sx={{ minWidth: { sm: 200 } }}><InputLabel>Categoria</InputLabel><Select label="Categoria" value={categories.some(item=>item.id===categoryId)?categoryId:''} onChange={event=>setCategory(event.target.value)}><MenuItem value="">Todas</MenuItem>{categories.map(category=><MenuItem key={category.id} value={category.id}>{category.name}</MenuItem>)}</Select></FormControl>
      <FormControl size="small" sx={{ minWidth: { sm: 210 } }}><InputLabel>Status</InputLabel><Select label="Status" value={status} onChange={(event) => setStatus(event.target.value as RequestStatus | '')}><MenuItem value="">Todos</MenuItem>{Object.entries(statusPresentation).map(([value, item]) => <MenuItem key={value} value={value}>{item.label}</MenuItem>)}</Select></FormControl>
      <FormControl size="small" sx={{ minWidth: { sm: 180 } }}><InputLabel>Prioridade</InputLabel><Select label="Prioridade" value={priority} onChange={(event) => setPriority(event.target.value as RequestPriority | '')}><MenuItem value="">Todas</MenuItem>{Object.entries(priorityPresentation).map(([value, item]) => <MenuItem key={value} value={value}>{item.label}</MenuItem>)}</Select></FormControl>
      <Button startIcon={<FilterAltOffRoundedIcon />} onClick={clearFilters} disabled={!status && !priority && !search && !categoryId}>Limpar filtros</Button>
    </Stack>
    {error && <Alert severity="error" action={<Button color="inherit" onClick={() => void load()}>Tentar novamente</Button>}>{error}</Alert>}
    {isLoading ? <Grid container spacing={2}>{[1, 2].map((item) => <Grid key={item} size={{ xs: 12, lg: 6 }}><Skeleton variant="rounded" height={170} /></Grid>)}</Grid> : data && visibleItems.length === 0 ? <EmptyState title={status || priority || search || categoryId ? 'Nenhuma solicitação encontrada com os filtros selecionados.' : 'Nenhuma solicitação ativa encontrada.'} description={status || priority || search || categoryId ? 'Revise ou limpe os filtros para consultar outros atendimentos.' : 'Os novos atendimentos aparecerão aqui.'} action={status || priority || search || categoryId ? <Button variant="contained" onClick={clearFilters}>Limpar filtros</Button> : undefined} /> : data && <Grid container spacing={2}>{visibleItems.map((request) => <Grid key={request.id} size={{ xs: 12, lg: 6 }}><ManagementRequestCard request={request} /></Grid>)}</Grid>}
  </PageContainer>
}
