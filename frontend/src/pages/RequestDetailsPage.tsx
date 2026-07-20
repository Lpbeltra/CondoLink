import { useCallback, useEffect, useRef, useState } from 'react'
import ArrowBackRoundedIcon from '@mui/icons-material/ArrowBackRounded'
import { Alert, Button, Card, CardContent, Divider, Grid, Skeleton, Stack, Typography } from '@mui/material'
import { useLocation, useNavigate, useParams } from 'react-router-dom'
import { PageContainer } from '../components/PageContainer'
import { useCondominium } from '../condominiums/CondominiumContext'
import { getRequest, listRequestMessages } from '../requests/api'
import { RequestConversation } from '../requests/components/RequestConversation'
import { RequestPriorityChip } from '../requests/components/RequestPriorityChip'
import { RequestStatusChip } from '../requests/components/RequestStatusChip'
import { RequestTimeline } from '../requests/components/RequestTimeline'
import { formatDateTime, getRequestError } from '../requests/presentation'
import type { RequestDetails, RequestMessage } from '../requests/types'
import { RequestManagementActions } from '../requests/components/RequestManagementActions'
import { RequestAttachments } from '../requests/components/RequestAttachments'

interface RequestDetailsPageProps {
  managementCondominiumId?: string | null
  managementMode?: boolean
}

export function RequestDetailsPage({ managementCondominiumId, managementMode = false }: RequestDetailsPageProps = {}) {
  const { requestId = '' } = useParams()
  const navigate = useNavigate()
  const location = useLocation()
  const { currentCondominium, isManager } = useCondominium()
  const [details, setDetails] = useState<RequestDetails | null>(null)
  const [messages, setMessages] = useState<RequestMessage[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState('')
  const loadVersion = useRef(0)
  const expectedCondominiumId = managementMode ? managementCondominiumId : currentCondominium?.condominium.id
  const returnPath = managementMode || (location.state as { fromManagement?: boolean } | null)?.fromManagement ? '/management/requests' : '/requests'

  const load = useCallback(async () => {
    const version = ++loadVersion.current
    if (!managementMode && !expectedCondominiumId) { setDetails(null); setMessages([]); setIsLoading(false); return }
    setIsLoading(true); setError(''); setDetails(null); setMessages([])
    try {
      const [request, conversation] = await Promise.all([getRequest(requestId), listRequestMessages(requestId)])
      if (version !== loadVersion.current) return
      setDetails(request); setMessages(conversation)
    } catch (requestError) { if (version === loadVersion.current) setError(getRequestError(requestError)) }
    finally { if (version === loadVersion.current) setIsLoading(false) }
  }, [expectedCondominiumId, managementMode, requestId])

  useEffect(() => { void load() }, [load])
  const wrongContext = !managementMode && details && details.condominiumId !== expectedCondominiumId

  if (isLoading) return <PageContainer><Skeleton variant="rounded" height={420} /></PageContainer>
  if (error) return <PageContainer><Button startIcon={<ArrowBackRoundedIcon />} onClick={() => navigate(returnPath)}>Voltar</Button><Alert severity="error" sx={{ mt: 2 }} action={<Button color="inherit" onClick={() => void load()}>Tentar novamente</Button>}>{error}</Alert></PageContainer>
  if (wrongContext) return <PageContainer><Alert severity="warning">Esta solicitação pertence a outro condomínio.</Alert><Button sx={{ mt: 2 }} onClick={() => navigate(returnPath)}>Voltar para solicitações</Button></PageContainer>
  if (!details) return null

  const unit = details.targetUnit && `${details.targetUnit.block ? `Bloco ${details.targetUnit.block} · ` : ''}${details.targetUnit.identifier}`
  return (
    <PageContainer>
      <Button startIcon={<ArrowBackRoundedIcon />} color="inherit" onClick={() => navigate(returnPath)} sx={{ mb: 2 }}>Voltar</Button>
      {(location.state as { created?: boolean } | null)?.created && <Alert severity="success" sx={{ mb: 2 }}>Solicitação aberta com sucesso.</Alert>}
      <Grid container spacing={3}>
        <Grid size={{ xs: 12, lg: 8 }}>
          <Card elevation={0}><CardContent sx={{ p: { xs: 2.5, sm: 4 } }}>
            <Stack direction="row" flexWrap="wrap" gap={1} mb={2}><RequestStatusChip status={details.status} /><RequestPriorityChip priority={details.priority} /></Stack>
            <Typography variant="h1">{details.title}</Typography>
            <Typography color="text.secondary" mt={1}>{details.category.name} · aberta em {formatDateTime(details.createdAt)}</Typography>
            <Divider sx={{ my: 3 }} />
            <Typography variant="h3" mb={1}>Descrição</Typography><Typography sx={{ whiteSpace: 'pre-wrap', overflowWrap: 'anywhere' }}>{details.description}</Typography>
            {unit && <><Divider sx={{ my: 3 }} /><Typography variant="h3" mb={1}>Unidade relacionada</Typography><Typography>{unit}</Typography></>}
          </CardContent></Card>
          {(managementMode || (isManager && details.condominiumId === expectedCondominiumId)) && <RequestManagementActions requestId={details.id} status={details.status} priority={details.priority} onUpdated={load} />}
          <RequestAttachments requestId={details.id} cancelled={details.status === 'Cancelled'} />
          <Card elevation={0} sx={{ mt: 3 }}><CardContent sx={{ p: { xs: 2.5, sm: 4 } }}><Typography variant="h2" mb={.5}>Atualizações</Typography><Typography color="text.secondary" mb={3}>Registre novas informações e acompanhe o atendimento.</Typography><RequestConversation requestId={details.id} status={details.status} messages={messages} onMessageCreated={(message) => setMessages((current) => [...current, message])} /></CardContent></Card>
        </Grid>
        <Grid size={{ xs: 12, lg: 4 }}><Card elevation={0}><CardContent sx={{ p: { xs: 2.5, sm: 3 } }}><Typography variant="h2" mb={3}>Histórico de status</Typography><RequestTimeline history={details.statusHistory} /></CardContent></Card></Grid>
      </Grid>
    </PageContainer>
  )
}

export function ManagementRequestDetailsPage() {
  return <RequestDetailsPage managementMode />
}
