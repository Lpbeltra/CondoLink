import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import axios from 'axios'
import FilterAltOffRoundedIcon from '@mui/icons-material/FilterAltOffRounded'
import SearchRoundedIcon from '@mui/icons-material/SearchRounded'
import { Alert, Box, Button, Card, CardActionArea, CardContent, FormControl, Grid, InputAdornment, InputLabel, MenuItem, Select, Skeleton, TextField, Typography } from '@mui/material'
import { useSearchParams } from 'react-router-dom'
import { EmptyState } from '../components/EmptyState'
import { PageContainer } from '../components/PageContainer'
import { useManagementContext } from '../management/ManagementContext'
import { ManagementCondominiumSwitcher } from '../management/components/ManagementCondominiumSwitcher'
import { listManagementRequests } from '../requests/api'
import { ManagementRequestCard } from '../requests/components/ManagementRequestCard'
import { applySummaryFilter, selectManagementRequests, sortManagementRequests } from '../requests/managementRequests'
import { getRequestError, priorityPresentation, statusPresentation } from '../requests/presentation'
import type { ManagementRequestsResponse, RequestStatus } from '../requests/types'
import { clearManagementRequestFilters, parseManagementRequestFilters, setManagementRequestFilter, syncCondominiumFilter } from '../requests/managementRequestFilters'
import { useVisiblePolling } from '../hooks/useVisiblePolling'

const summaries = [
  ['Abertas', 'open', 'Open'], ['Em andamento', 'inProgress', 'InProgress'], ['Aguardando morador', 'waitingForResident', 'WaitingForResident'], ['Dar andamento', 'waitingForManager', 'WaitingForManager'], ['Aguardando terceiro', 'waitingForThirdParty', 'WaitingForThirdParty'], ['Resolvidas', 'resolved', 'Resolved'], ['Canceladas', 'cancelled', 'Cancelled'],
] as const

export function ManagementRequestsPage() {
  const {
    activeCondominiumId,
    activeCondominium,
    usesConsolidatedManagementScope,
    condominiums,
    isLoading: isManagementLoading,
    selectCondominium,
    refresh: refreshManagementContext,
  } = useManagementContext()
  const [searchParams,setSearchParams] = useSearchParams()
  const filters = useMemo(
    () => parseManagementRequestFilters(searchParams),
    [searchParams],
  )
  const requestedCategoryId = filters.categoryId
  const categoryId = requestedCategoryId
  const { status, priority, search, sort, direction } = filters
  const [data, setData] = useState<ManagementRequestsResponse | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState('')
  const loadVersion = useRef(0)
  const urlContextInitialized = useRef(false)
  const selectingUrlContext = useRef(false)

  useEffect(() => {
    if (isManagementLoading || urlContextInitialized.current) return
    urlContextInitialized.current = true
    const requested = filters.condominiumId
    if (requested
      && condominiums.some(item => item.id === requested)
      && requested !== activeCondominiumId) {
      selectingUrlContext.current = true
      void selectCondominium(requested)
      return
    }
    const next = syncCondominiumFilter(
      searchParams,
      activeCondominiumId,
    )
    if (next.toString() !== searchParams.toString()) {
      setSearchParams(next, { replace: true })
    }
  }, [activeCondominiumId, condominiums, filters.condominiumId, isManagementLoading, searchParams, selectCondominium, setSearchParams])

  useEffect(() => {
    if (!urlContextInitialized.current || isManagementLoading) return
    if (selectingUrlContext.current) {
      if (filters.condominiumId !== activeCondominiumId) return
      selectingUrlContext.current = false
    }
    const next = syncCondominiumFilter(
      searchParams,
      activeCondominiumId,
    )
    if (next.toString() !== searchParams.toString()) {
      setSearchParams(next, { replace: true })
    }
  }, [activeCondominiumId, filters.condominiumId, isManagementLoading, searchParams, setSearchParams])

  // The active condominium is both sent to the API and kept as a dependency:
  // without it, switching condominium left another tenant's requests on screen.
  const load = useCallback(async (silent = false) => {
    const version = ++loadVersion.current
    if (!silent) { setIsLoading(true); setError(''); setData(null) }
    try { const result = await listManagementRequests({ status: status || undefined, priority: priority || undefined, condominiumId: activeCondominiumId ?? undefined }); if (version === loadVersion.current) setData(result) }
    catch (requestError) {
      if (version !== loadVersion.current) return
      if (axios.isAxiosError(requestError) &&
        [403, 404, 409].includes(requestError.response?.status ?? 0)) {
        await refreshManagementContext()
      }
      if (!silent && version === loadVersion.current) setError(getRequestError(requestError))
    }
    finally { if (!silent && version === loadVersion.current) setIsLoading(false) }
  }, [activeCondominiumId, priority, refreshManagementContext, status])

  useEffect(() => { void load() }, [load])
  const poll = useCallback(() => { void load(true) }, [load])
  useVisiblePolling(poll)
  const setFilter=(key:'status'|'priority'|'categoryId'|'search'|'sort'|'direction',value:string)=>setSearchParams(setManagementRequestFilter(searchParams,key,value))
  const setCategory=(value:string)=>setFilter('categoryId',value)
  const clearFilters = () => setSearchParams(clearManagementRequestFilters(searchParams))
  const selectSummary = (selectedStatus: RequestStatus) => {
    const filters = applySummaryFilter(selectedStatus, search)
    let next=setManagementRequestFilter(searchParams,'status',filters.status)
    next=setManagementRequestFilter(next,'priority',filters.priority)
    setSearchParams(next)
  }
  const categories = data ? Array.from(new Map(data.items.map(item => [item.category.id, item.category])).values()).sort((a,b)=>a.name.localeCompare(b.name,'pt-BR')) : []
  const applicableCategoryId = !usesConsolidatedManagementScope &&
    categories.some(item => item.id === categoryId) ? categoryId : ''
  const visibleItems = data ? sortManagementRequests(selectManagementRequests(data.items, status, search).filter(item => !applicableCategoryId || item.category.id === applicableCategoryId), sort, direction) : []

  return <PageContainer maxWidth={1440} sx={{ overflowX: 'hidden' }}>
    <Typography variant="h1">Atendimento</Typography>
    <Typography color="text.secondary" mt={.5}>
      {usesConsolidatedManagementScope
        ? 'Acompanhe solicitações de todos os condomínios administrados.'
        : `Acompanhe e organize as solicitações de ${activeCondominium?.name ?? 'seu condomínio'}.`}
    </Typography>
    {/* This page bypasses ManagementLayout, so it carries its own switcher. */}
    <Box mt={2} maxWidth={360}>
      <ManagementCondominiumSwitcher />
    </Box>
    {isLoading ? <Skeleton variant="rounded" height={120} sx={{ mt: 3 }} /> : data && <Box display="grid" gridTemplateColumns="repeat(auto-fit, minmax(132px, 1fr))" gap={1.5} mt={3}>{summaries.map(([label, key, summaryStatus]) => <Card key={key} elevation={0} sx={{ minWidth: 0, boxShadow: 'none', border: '1px solid', borderColor: status === summaryStatus ? 'primary.main' : 'divider', bgcolor: status === summaryStatus ? 'rgba(31,94,255,.045)' : 'background.paper' }}><CardActionArea onClick={() => selectSummary(summaryStatus)} aria-label={`Filtrar por ${label}`} sx={{ height: '100%', borderRadius: 'inherit', '&:focus-visible': { outline: '3px solid', outlineColor: 'primary.light', outlineOffset: -3 } }}><CardContent sx={{ p: 2, '&:last-child': { pb: 2 } }}><Typography color="text.secondary" fontSize=".75rem" fontWeight={700}>{label}</Typography><Typography variant="h2" mt={.5}>{data.counts[key]}</Typography></CardContent></CardActionArea></Card>)}</Box>}
    <Box display="grid" gridTemplateColumns={{ xs: 'minmax(0, 1fr)', sm: 'repeat(2, minmax(0, 1fr))', lg: 'repeat(4, minmax(0, 1fr))', xl: 'repeat(6, minmax(0, 1fr))' }} gap={1.5} my={3} alignItems="center">
      <TextField size="small" label="Buscar" value={search} onChange={(event) => setFilter('search',event.target.value)} placeholder="Título, morador, categoria ou unidade" sx={{ minWidth: 0, gridColumn: { sm: 'span 2', lg: 'span 2', xl: 'span 1' } }} InputProps={{ startAdornment: <InputAdornment position="start"><SearchRoundedIcon /></InputAdornment> }} />
      {!usesConsolidatedManagementScope && <FormControl size="small" sx={{ minWidth: 0 }}><InputLabel id="request-category-label">Categoria</InputLabel><Select labelId="request-category-label" label="Categoria" value={applicableCategoryId} onChange={event=>setCategory(event.target.value)}><MenuItem value="">Todas</MenuItem>{categories.map(category=><MenuItem key={category.id} value={category.id}>{category.name}</MenuItem>)}</Select></FormControl>}
      <FormControl size="small" sx={{ minWidth: 0 }}><InputLabel id="request-status-label">Status</InputLabel><Select labelId="request-status-label" label="Status" value={status} onChange={(event) => setFilter('status',event.target.value)}><MenuItem value="">Todos</MenuItem>{Object.entries(statusPresentation).map(([value, item]) => <MenuItem key={value} value={value}>{item.label}</MenuItem>)}</Select></FormControl>
      <FormControl size="small" sx={{ minWidth: 0 }}><InputLabel id="request-priority-label">Prioridade</InputLabel><Select labelId="request-priority-label" label="Prioridade" value={priority} onChange={(event) => setFilter('priority',event.target.value)}><MenuItem value="">Todas</MenuItem>{Object.entries(priorityPresentation).map(([value, item]) => <MenuItem key={value} value={value}>{item.label}</MenuItem>)}</Select></FormControl>
      <FormControl size="small" sx={{ minWidth: 0 }}><InputLabel id="request-sort-label">Ordenar por</InputLabel><Select labelId="request-sort-label" label="Ordenar por" value={sort} onChange={event=>setFilter('sort',event.target.value)}><MenuItem value="createdAt">Data de abertura</MenuItem><MenuItem value="priority">Urgência</MenuItem><MenuItem value="condominium">Condomínio</MenuItem></Select></FormControl>
      <FormControl size="small" sx={{ minWidth: 0 }}><InputLabel id="request-direction-label">Ordem</InputLabel><Select labelId="request-direction-label" label="Ordem" value={direction} onChange={event=>setFilter('direction',event.target.value)}><MenuItem value="asc">Crescente</MenuItem><MenuItem value="desc">Decrescente</MenuItem></Select></FormControl>
      <Button sx={{ justifySelf: { xs: 'stretch', sm: 'start' } }} startIcon={<FilterAltOffRoundedIcon />} onClick={clearFilters} disabled={!status && !priority && !search && !applicableCategoryId}>Limpar filtros</Button>
    </Box>
    {error && <Alert severity="error" action={<Button color="inherit" onClick={() => void load()}>Tentar novamente</Button>}>{error}</Alert>}
    {isLoading ? <Grid container spacing={2}>{[1, 2].map((item) => <Grid key={item} size={{ xs: 12, lg: 6 }}><Skeleton variant="rounded" height={170} /></Grid>)}</Grid> : data && visibleItems.length === 0 ? <EmptyState title={status || priority || search || applicableCategoryId ? 'Nenhuma solicitação encontrada com os filtros selecionados.' : 'Nenhuma solicitação ativa encontrada.'} description={status || priority || search || applicableCategoryId ? 'Revise ou limpe os filtros para consultar outros atendimentos.' : 'Os novos atendimentos aparecerão aqui.'} action={status || priority || search || applicableCategoryId ? <Button variant="contained" onClick={clearFilters}>Limpar filtros</Button> : undefined} /> : data && <Grid container spacing={2}>{visibleItems.map((request) => <Grid key={request.id} size={{ xs: 12, lg: 6 }}><ManagementRequestCard request={request} /></Grid>)}</Grid>}
  </PageContainer>
}
