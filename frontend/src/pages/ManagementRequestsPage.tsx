import { useCallback, useEffect, useRef, useState } from 'react'
import FilterAltOffRoundedIcon from '@mui/icons-material/FilterAltOffRounded'
import SearchRoundedIcon from '@mui/icons-material/SearchRounded'
import { Alert, Box, Button, Card, CardActionArea, CardContent, FormControl, Grid, InputAdornment, InputLabel, MenuItem, Select, Skeleton, TextField, Typography } from '@mui/material'
import { useSearchParams } from 'react-router-dom'
import { EmptyState } from '../components/EmptyState'
import { PageContainer } from '../components/PageContainer'
import { listManagementRequests } from '../requests/api'
import { ManagementRequestCard } from '../requests/components/ManagementRequestCard'
import { applySummaryFilter, selectManagementRequests, sortManagementRequests, type ManagementRequestSort, type SortDirection } from '../requests/managementRequests'
import { getRequestError, priorityPresentation, statusPresentation } from '../requests/presentation'
import type { ManagementRequestsResponse, RequestPriority, RequestStatus } from '../requests/types'

const summaries = [
  ['Abertas', 'open', 'Open'], ['Em andamento', 'inProgress', 'InProgress'], ['Aguardando morador', 'waitingForResident', 'WaitingForResident'], ['Aguardando terceiro', 'waitingForThirdParty', 'WaitingForThirdParty'], ['Resolvidas', 'resolved', 'Resolved'], ['Canceladas', 'cancelled', 'Cancelled'],
] as const

export function ManagementRequestsPage() {
  const [searchParams,setSearchParams] = useSearchParams()
  const requestedCategoryId = searchParams.get('categoryId') ?? ''
  const categoryId = requestedCategoryId
  const [status, setStatus] = useState<RequestStatus | ''>('')
  const [priority, setPriority] = useState<RequestPriority | ''>('')
  const [search, setSearch] = useState('')
  const [data, setData] = useState<ManagementRequestsResponse | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState('')
  const [sort, setSort] = useState<ManagementRequestSort>('createdAt')
  const [direction, setDirection] = useState<SortDirection>('desc')
  const loadVersion = useRef(0)

  const load = useCallback(async () => {
    const version = ++loadVersion.current
    setIsLoading(true); setError(''); setData(null)
    try { const result = await listManagementRequests({ status: status || undefined, priority: priority || undefined }); if (version === loadVersion.current) setData(result) }
    catch (requestError) { if (version === loadVersion.current) setError(getRequestError(requestError)) }
    finally { if (version === loadVersion.current) setIsLoading(false) }
  }, [priority, status])

  useEffect(() => { void load() }, [load])
  const setCategory=(value:string)=>{const next=new URLSearchParams(searchParams);if(value)next.set('categoryId',value);else next.delete('categoryId');setSearchParams(next)}
  const clearFilters = () => { setStatus(''); setPriority(''); setSearch(''); setCategory('') }
  const selectSummary = (selectedStatus: RequestStatus) => {
    const filters = applySummaryFilter(selectedStatus, search)
    setStatus(filters.status); setPriority(filters.priority)
  }
  const categories = data ? Array.from(new Map(data.items.map(item => [item.category.id, item.category])).values()).sort((a,b)=>a.name.localeCompare(b.name,'pt-BR')) : []
  const visibleItems = data ? sortManagementRequests(selectManagementRequests(data.items, status, search).filter(item => !categoryId || item.category.id === categoryId), sort, direction) : []

  return <PageContainer maxWidth={1440} sx={{ overflowX: 'hidden' }}>
    <Typography variant="h1">Atendimento</Typography><Typography color="text.secondary" mt={.5}>Acompanhe e organize as solicitações do condomínio.</Typography>
    {isLoading ? <Skeleton variant="rounded" height={120} sx={{ mt: 3 }} /> : data && <Box display="grid" gridTemplateColumns={{ xs: 'repeat(2, minmax(0, 1fr))', md: 'repeat(3, minmax(0, 1fr))', xl: 'repeat(6, minmax(0, 1fr))' }} gap={1.5} mt={3}>{summaries.map(([label, key, summaryStatus]) => <Card key={key} elevation={0} sx={{ minWidth: 0, boxShadow: 'none', border: '1px solid', borderColor: status === summaryStatus ? 'primary.main' : 'divider', bgcolor: status === summaryStatus ? 'rgba(31,94,255,.045)' : 'background.paper' }}><CardActionArea onClick={() => selectSummary(summaryStatus)} aria-label={`Filtrar por ${label}`} sx={{ height: '100%', borderRadius: 'inherit', '&:focus-visible': { outline: '3px solid', outlineColor: 'primary.light', outlineOffset: -3 } }}><CardContent sx={{ p: 2, '&:last-child': { pb: 2 } }}><Typography color="text.secondary" fontSize=".75rem" fontWeight={700}>{label}</Typography><Typography variant="h2" mt={.5}>{data.counts[key]}</Typography></CardContent></CardActionArea></Card>)}</Box>}
    <Box display="grid" gridTemplateColumns={{ xs: 'minmax(0, 1fr)', sm: 'repeat(2, minmax(0, 1fr))', lg: 'repeat(4, minmax(0, 1fr))', xl: 'repeat(6, minmax(0, 1fr))' }} gap={1.5} my={3} alignItems="center">
      <TextField size="small" label="Buscar" value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Título, morador, categoria ou unidade" sx={{ minWidth: 0, gridColumn: { sm: 'span 2', lg: 'span 2', xl: 'span 1' } }} InputProps={{ startAdornment: <InputAdornment position="start"><SearchRoundedIcon /></InputAdornment> }} />
      <FormControl size="small" sx={{ minWidth: 0 }}><InputLabel>Categoria</InputLabel><Select label="Categoria" value={categories.some(item=>item.id===categoryId)?categoryId:''} onChange={event=>setCategory(event.target.value)}><MenuItem value="">Todas</MenuItem>{categories.map(category=><MenuItem key={category.id} value={category.id}>{category.name}</MenuItem>)}</Select></FormControl>
      <FormControl size="small" sx={{ minWidth: 0 }}><InputLabel>Status</InputLabel><Select label="Status" value={status} onChange={(event) => setStatus(event.target.value as RequestStatus | '')}><MenuItem value="">Todos</MenuItem>{Object.entries(statusPresentation).map(([value, item]) => <MenuItem key={value} value={value}>{item.label}</MenuItem>)}</Select></FormControl>
      <FormControl size="small" sx={{ minWidth: 0 }}><InputLabel>Prioridade</InputLabel><Select label="Prioridade" value={priority} onChange={(event) => setPriority(event.target.value as RequestPriority | '')}><MenuItem value="">Todas</MenuItem>{Object.entries(priorityPresentation).map(([value, item]) => <MenuItem key={value} value={value}>{item.label}</MenuItem>)}</Select></FormControl>
      <FormControl size="small" sx={{ minWidth: 0 }}><InputLabel>Ordenar por</InputLabel><Select label="Ordenar por" value={sort} onChange={event=>setSort(event.target.value as ManagementRequestSort)}><MenuItem value="createdAt">Data de abertura</MenuItem><MenuItem value="priority">Urgência</MenuItem><MenuItem value="condominium">Condomínio</MenuItem></Select></FormControl>
      <FormControl size="small" sx={{ minWidth: 0 }}><InputLabel>Ordem</InputLabel><Select label="Ordem" value={direction} onChange={event=>setDirection(event.target.value as SortDirection)}><MenuItem value="asc">Crescente</MenuItem><MenuItem value="desc">Decrescente</MenuItem></Select></FormControl>
      <Button sx={{ justifySelf: { xs: 'stretch', sm: 'start' } }} startIcon={<FilterAltOffRoundedIcon />} onClick={clearFilters} disabled={!status && !priority && !search && !categoryId}>Limpar filtros</Button>
    </Box>
    {error && <Alert severity="error" action={<Button color="inherit" onClick={() => void load()}>Tentar novamente</Button>}>{error}</Alert>}
    {isLoading ? <Grid container spacing={2}>{[1, 2].map((item) => <Grid key={item} size={{ xs: 12, lg: 6 }}><Skeleton variant="rounded" height={170} /></Grid>)}</Grid> : data && visibleItems.length === 0 ? <EmptyState title={status || priority || search || categoryId ? 'Nenhuma solicitação encontrada com os filtros selecionados.' : 'Nenhuma solicitação ativa encontrada.'} description={status || priority || search || categoryId ? 'Revise ou limpe os filtros para consultar outros atendimentos.' : 'Os novos atendimentos aparecerão aqui.'} action={status || priority || search || categoryId ? <Button variant="contained" onClick={clearFilters}>Limpar filtros</Button> : undefined} /> : data && <Grid container spacing={2}>{visibleItems.map((request) => <Grid key={request.id} size={{ xs: 12, lg: 6 }}><ManagementRequestCard request={request} /></Grid>)}</Grid>}
  </PageContainer>
}
