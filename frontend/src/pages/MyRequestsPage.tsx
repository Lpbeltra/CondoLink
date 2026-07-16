import { useCallback, useEffect, useMemo, useState } from 'react'
import AddRoundedIcon from '@mui/icons-material/AddRounded'
import { Alert, Box, Button, Fab, Grid, Skeleton, Typography } from '@mui/material'
import { useNavigate } from 'react-router-dom'
import { EmptyState } from '../components/EmptyState'
import { PageContainer } from '../components/PageContainer'
import { useCondominium } from '../condominiums/CondominiumContext'
import { listMyRequests } from '../requests/api'
import { RequestCard } from '../requests/components/RequestCard'
import { filterRequestsByCondominium, getRequestError } from '../requests/presentation'
import type { RequestListItem } from '../requests/types'

export function MyRequestsPage() {
  const navigate = useNavigate()
  const { currentCondominium } = useCondominium()
  const [requests, setRequests] = useState<RequestListItem[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState('')

  const load = useCallback(async () => {
    setIsLoading(true); setError(''); setRequests([])
    try { setRequests(await listMyRequests()) }
    catch (requestError) { setError(getRequestError(requestError)) }
    finally { setIsLoading(false) }
  }, [])

  useEffect(() => { void load() }, [load, currentCondominium?.condominium.id])
  const visibleRequests = useMemo(() => currentCondominium ? filterRequestsByCondominium(requests, currentCondominium.condominium.id) : [], [currentCondominium, requests])

  return (
    <PageContainer>
      <Box display="flex" justifyContent="space-between" alignItems="flex-start" gap={2} mb={3}>
        <Box><Typography variant="h1">Solicitações</Typography><Typography color="text.secondary" mt={.5}>Acompanhe suas conversas com a administração.</Typography></Box>
        <Button variant="contained" startIcon={<AddRoundedIcon />} onClick={() => navigate('/requests/new')} sx={{ display: { xs: 'none', sm: 'inline-flex' } }}>Nova solicitação</Button>
      </Box>
      {error && <Alert severity="error" action={<Button color="inherit" onClick={() => void load()}>Tentar novamente</Button>}>{error}</Alert>}
      {isLoading ? <Grid container spacing={2}>{[1, 2].map((item) => <Grid key={item} size={{ xs: 12, lg: 6 }}><Skeleton variant="rounded" height={170} /></Grid>)}</Grid>
        : !error && visibleRequests.length === 0 ? <EmptyState title="Nenhuma solicitação por aqui" description="Quando precisar falar com a administração, abra uma nova solicitação." action={<Button variant="contained" onClick={() => navigate('/requests/new')}>Abrir solicitação</Button>} />
          : <Grid container spacing={2}>{visibleRequests.map((request) => <Grid key={request.id} size={{ xs: 12, lg: 6 }}><RequestCard request={request} /></Grid>)}</Grid>}
      <Fab color="primary" aria-label="Nova solicitação" onClick={() => navigate('/requests/new')} sx={{ display: { sm: 'none' }, position: 'fixed', right: 20, bottom: 'calc(76px + env(safe-area-inset-bottom))' }}><AddRoundedIcon /></Fab>
    </PageContainer>
  )
}
