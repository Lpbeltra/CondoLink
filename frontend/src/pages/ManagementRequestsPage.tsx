import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import axios from 'axios'
import FilterAltOffRoundedIcon from '@mui/icons-material/FilterAltOffRounded'
import FilterAltRoundedIcon from '@mui/icons-material/FilterAltRounded'
import SearchRoundedIcon from '@mui/icons-material/SearchRounded'
import { Alert, Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, FormControl, Grid, InputAdornment, InputLabel, MenuItem, Select, Skeleton, Stack, TextField, useMediaQuery, useTheme } from '@mui/material'
import { useSearchParams } from 'react-router-dom'
import { EmptyState } from '../components/EmptyState'
import { PageContainer } from '../components/PageContainer'
import { PageHeader } from '../components/PageHeader'
import { useManagementContext } from '../management/ManagementContext'
import { listManagementRequests } from '../requests/api'
import { ManagementRequestCard } from '../requests/components/ManagementRequestCard'
import { OperationalSummary } from '../requests/components/OperationalSummary'
import { applySummaryFilter, selectManagementRequests, sortManagementRequests } from '../requests/managementRequests'
import { getRequestError, priorityPresentation, statusPresentation } from '../requests/presentation'
import type { ManagementRequestsResponse, RequestStatus } from '../requests/types'
import { clearManagementRequestFilters, parseManagementRequestFilters, setManagementRequestFilter, syncCondominiumFilter } from '../requests/managementRequestFilters'
import { useVisiblePolling } from '../hooks/useVisiblePolling'

export function ManagementRequestsPage() {
  const theme = useTheme()
  const isMobile = useMediaQuery(theme.breakpoints.down('md'))
  const { activeCondominiumId, activeCondominium, usesConsolidatedManagementScope, condominiums, isLoading: isManagementLoading, selectCondominium, refresh: refreshManagementContext } = useManagementContext()
  const [searchParams, setSearchParams] = useSearchParams()
  const filters = useMemo(() => parseManagementRequestFilters(searchParams), [searchParams])
  const { status, priority, search, sort, direction } = filters
  const [data, setData] = useState<ManagementRequestsResponse | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState('')
  const [filtersOpen, setFiltersOpen] = useState(false)
  const loadVersion = useRef(0)
  const urlContextInitialized = useRef(false)
  const selectingUrlContext = useRef(false)

  useEffect(() => {
    if (isManagementLoading || urlContextInitialized.current) return
    urlContextInitialized.current = true
    const requested = filters.condominiumId
    if (requested && condominiums.some(item => item.id === requested) && requested !== activeCondominiumId) {
      selectingUrlContext.current = true
      void selectCondominium(requested)
      return
    }
    const next = syncCondominiumFilter(searchParams, activeCondominiumId)
    if (next.toString() !== searchParams.toString()) setSearchParams(next, { replace: true })
  }, [activeCondominiumId, condominiums, filters.condominiumId, isManagementLoading, searchParams, selectCondominium, setSearchParams])

  useEffect(() => {
    if (!urlContextInitialized.current || isManagementLoading) return
    if (selectingUrlContext.current) {
      if (filters.condominiumId !== activeCondominiumId) return
      selectingUrlContext.current = false
    }
    const next = syncCondominiumFilter(searchParams, activeCondominiumId)
    if (next.toString() !== searchParams.toString()) setSearchParams(next, { replace: true })
  }, [activeCondominiumId, filters.condominiumId, isManagementLoading, searchParams, setSearchParams])

  const load = useCallback(async (silent = false) => {
    const version = ++loadVersion.current
    if (!silent) { setIsLoading(true); setError(''); setData(null) }
    try {
      const result = await listManagementRequests({ status: status || undefined, priority: priority || undefined, condominiumId: activeCondominiumId ?? undefined })
      if (version === loadVersion.current) setData(result)
    } catch (requestError) {
      if (version !== loadVersion.current) return
      if (axios.isAxiosError(requestError) && [403, 404, 409].includes(requestError.response?.status ?? 0)) await refreshManagementContext()
      if (!silent && version === loadVersion.current) setError(getRequestError(requestError))
    } finally { if (!silent && version === loadVersion.current) setIsLoading(false) }
  }, [activeCondominiumId, priority, refreshManagementContext, status])

  useEffect(() => { void load() }, [load])
  useVisiblePolling(useCallback(() => load(true), [load]))
  const setFilter = (key: 'status' | 'priority' | 'categoryId' | 'search' | 'sort' | 'direction', value: string) => setSearchParams(setManagementRequestFilter(searchParams, key, value))
  const clearFilters = () => setSearchParams(clearManagementRequestFilters(searchParams))
  const selectSummary = (selectedStatus: RequestStatus) => {
    const nextFilters = applySummaryFilter(selectedStatus, search)
    let next = setManagementRequestFilter(searchParams, 'status', nextFilters.status)
    next = setManagementRequestFilter(next, 'priority', nextFilters.priority)
    setSearchParams(next)
  }
  const categories = data ? Array.from(new Map(data.items.map(item => [item.category.id, item.category])).values()).sort((a, b) => a.name.localeCompare(b.name, 'pt-BR')) : []
  const applicableCategoryId = !usesConsolidatedManagementScope && categories.some(item => item.id === filters.categoryId) ? filters.categoryId : ''
  const visibleItems = data ? sortManagementRequests(selectManagementRequests(data.items, status, search).filter(item => !applicableCategoryId || item.category.id === applicableCategoryId), sort, direction) : []

  const detailedFilters = <Stack gap={1.25} sx={isMobile ? undefined : { display: 'contents' }}>
    {!usesConsolidatedManagementScope && <FormControl size="small" sx={{ minWidth: 0 }}><InputLabel id="request-category-label">Categoria</InputLabel><Select aria-label="Categoria" labelId="request-category-label" label="Categoria" value={applicableCategoryId} onChange={event => setFilter('categoryId', event.target.value)}><MenuItem value="">Todas</MenuItem>{categories.map(category => <MenuItem key={category.id} value={category.id}>{category.name}</MenuItem>)}</Select></FormControl>}
    <FormControl size="small" sx={{ minWidth: 0 }}><InputLabel id="request-status-label">Status</InputLabel><Select aria-label="Status" labelId="request-status-label" label="Status" value={status} onChange={event => setFilter('status', event.target.value)}><MenuItem value="">Todos</MenuItem>{Object.entries(statusPresentation).map(([value, item]) => <MenuItem key={value} value={value}>{item.label}</MenuItem>)}</Select></FormControl>
    <FormControl size="small" sx={{ minWidth: 0 }}><InputLabel id="request-priority-label">Prioridade</InputLabel><Select aria-label="Prioridade" labelId="request-priority-label" label="Prioridade" value={priority} onChange={event => setFilter('priority', event.target.value)}><MenuItem value="">Todas</MenuItem>{Object.entries(priorityPresentation).map(([value, item]) => <MenuItem key={value} value={value}>{item.label}</MenuItem>)}</Select></FormControl>
    <FormControl size="small" sx={{ minWidth: 0 }}><InputLabel id="request-sort-label">Ordenar por</InputLabel><Select aria-label="Ordenar por" labelId="request-sort-label" label="Ordenar por" value={sort} onChange={event => setFilter('sort', event.target.value)}><MenuItem value="createdAt">Data de abertura</MenuItem><MenuItem value="priority">Urgência</MenuItem><MenuItem value="condominium">Condomínio</MenuItem></Select></FormControl>
    <FormControl size="small" sx={{ minWidth: 0 }}><InputLabel id="request-direction-label">Ordem</InputLabel><Select aria-label="Ordem" labelId="request-direction-label" label="Ordem" value={direction} onChange={event => setFilter('direction', event.target.value)}><MenuItem value="asc">Crescente</MenuItem><MenuItem value="desc">Decrescente</MenuItem></Select></FormControl>
  </Stack>

  const requests = <Box data-testid="management-request-list">
    {error && <Alert severity="error" action={<Button color="inherit" onClick={() => void load()}>Tentar novamente</Button>}>{error}</Alert>}
    {isLoading ? <Grid container spacing={1.5}>{[1, 2].map(item => <Grid key={item} size={{ xs: 12, lg: 6 }}><Skeleton variant="rounded" height={132} /></Grid>)}</Grid> : data && visibleItems.length === 0 ? <EmptyState title={status || priority || search || applicableCategoryId ? 'Nenhuma solicitação encontrada com os filtros selecionados.' : 'Nenhuma solicitação ativa encontrada.'} description={status || priority || search || applicableCategoryId ? 'Revise ou limpe os filtros para consultar outros atendimentos.' : 'Os novos atendimentos aparecerão aqui.'} action={status || priority || search || applicableCategoryId ? <Button variant="contained" onClick={clearFilters}>Limpar filtros</Button> : undefined} /> : data && <Grid container spacing={1.25}>{visibleItems.map(request => <Grid key={request.id} size={{ xs: 12, lg: 6 }}><ManagementRequestCard request={request} /></Grid>)}</Grid>}
  </Box>

  const summary = <OperationalSummary counts={data?.counts} status={status} loading={isLoading} onSelect={selectSummary} />
  return <PageContainer maxWidth={1440} sx={{ overflowX: 'hidden' }}>
    <PageHeader title="Atendimento" description={usesConsolidatedManagementScope ? 'Acompanhe solicitações de todos os condomínios administrados.' : `Acompanhe e organize as solicitações de ${activeCondominium?.name ?? 'seu condomínio'}.`} />
    {isMobile ? <>
      <Stack direction="row" gap={1} mb={1.5}><TextField fullWidth size="small" label="Buscar atendimentos" value={search} onChange={event => setFilter('search', event.target.value)} placeholder="Título, morador, categoria ou unidade" InputProps={{ startAdornment: <InputAdornment position="start"><SearchRoundedIcon /></InputAdornment> }} /><Button variant="outlined" startIcon={<FilterAltRoundedIcon />} onClick={() => setFiltersOpen(true)}>Filtrar</Button></Stack>
      <Box data-testid="management-request-summary" mb={1.5}>{summary}</Box>{requests}
      <Dialog open={filtersOpen} onClose={() => setFiltersOpen(false)} fullWidth maxWidth="xs"><DialogTitle>Filtrar atendimentos</DialogTitle><DialogContent><Box pt={1}>{detailedFilters}</Box></DialogContent><DialogActions><Button startIcon={<FilterAltOffRoundedIcon />} onClick={clearFilters} disabled={!status && !priority && !applicableCategoryId}>Limpar</Button><Button variant="contained" onClick={() => setFiltersOpen(false)}>Concluir</Button></DialogActions></Dialog>
    </> : <>
      <Box data-testid="management-request-summary" mb={1.5}>{summary}</Box>
      <Box display="grid" gridTemplateColumns={{ sm: 'repeat(2, minmax(0, 1fr))', lg: 'repeat(4, minmax(0, 1fr))', xl: 'repeat(6, minmax(0, 1fr))' }} gap={1} mb={2} alignItems="center">
        <TextField size="small" label="Buscar" value={search} onChange={event => setFilter('search', event.target.value)} placeholder="Título, morador, categoria ou unidade" sx={{ minWidth: 0, gridColumn: { sm: 'span 2', lg: 'span 2', xl: 'span 1' } }} InputProps={{ startAdornment: <InputAdornment position="start"><SearchRoundedIcon /></InputAdornment> }} />
        {detailedFilters}<Button sx={{ justifySelf: 'start' }} startIcon={<FilterAltOffRoundedIcon />} onClick={clearFilters} disabled={!status && !priority && !search && !applicableCategoryId}>Limpar filtros</Button>
      </Box>{requests}
    </>}
  </PageContainer>
}
