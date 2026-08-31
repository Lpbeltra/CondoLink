import { useCallback, useEffect, useRef, useState } from 'react'
import ArrowBackRoundedIcon from '@mui/icons-material/ArrowBackRounded'
import { Alert, Box, Button, Card, CardContent, Divider, Grid, Skeleton, Stack, Typography } from '@mui/material'
import { useLocation, useNavigate, useParams } from 'react-router-dom'
import { PageContainer } from '../components/PageContainer'
import { useCondominium } from '../condominiums/CondominiumContext'
import { getRequest, listRequestMessages } from '../requests/api'
import { RequestConversation } from '../requests/components/RequestConversation'
import { RequestPriorityChip } from '../requests/components/RequestPriorityChip'
import { RequestStatusChip } from '../requests/components/RequestStatusChip'
import { RequestTimeline } from '../requests/components/RequestTimeline'
import { formatDateTime, formatRequestProtocol, getRequestError, isClosedRequest } from '../requests/presentation'
import type { RequestDetails, RequestMessage } from '../requests/types'
import { RequestManagementActions } from '../requests/components/RequestManagementActions'
import { RequestAttachments } from '../requests/components/RequestAttachments'
import { canViewInternalRequestDetails, RequestAiAssistant } from '../requests/components/RequestAiAssistant'
import { OriginalReportAccordion } from '../requests/components/OriginalReportAccordion'
import { ResidentReplyPanel } from '../requests/components/ResidentReplyPanel'
import { ResidentClosurePanel } from '../requests/components/ResidentClosurePanel'
import { ResidentUpdateAcknowledgement } from '../requests/components/ResidentUpdateAcknowledgement'
import { useVisiblePolling } from '../hooks/useVisiblePolling'
import { ResidentSummaryCard } from '../requests/components/ResidentSummaryCard'

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
  const [actionFeedback, setActionFeedback] = useState('')
  const loadVersion = useRef(0)
  const expectedCondominiumId = managementMode ? managementCondominiumId : currentCondominium?.condominium.id
  const returnPath = managementMode || (location.state as { fromManagement?: boolean } | null)?.fromManagement ? '/management/requests' : '/requests'

  const load = useCallback(async (silent = false) => {
    const version = ++loadVersion.current
    if (!managementMode && !expectedCondominiumId) { setDetails(null); setMessages([]); setIsLoading(false); return }
    if (!silent) { setIsLoading(true); setError(''); setDetails(null); setMessages([]) }
    try {
      const [request, conversation] = await Promise.all([getRequest(requestId), listRequestMessages(requestId)])
      if (version !== loadVersion.current) return
      setDetails(request); setMessages(conversation)
    } catch (requestError) { if (!silent && version === loadVersion.current) setError(getRequestError(requestError)) }
    finally { if (!silent && version === loadVersion.current) setIsLoading(false) }
  }, [expectedCondominiumId, managementMode, requestId])

  useEffect(() => { void load() }, [load])
  const poll = useCallback(() => load(true), [load])
  useVisiblePolling(poll)
  const wrongContext = !managementMode && details && details.condominiumId !== expectedCondominiumId

  if (isLoading) return <PageContainer><Skeleton variant="rounded" height={420} /></PageContainer>
  if (error) return <PageContainer><Button startIcon={<ArrowBackRoundedIcon />} onClick={() => navigate(returnPath)}>Voltar</Button><Alert severity="error" sx={{ mt: 2 }} action={<Button color="inherit" onClick={() => void load()}>Tentar novamente</Button>}>{error}</Alert></PageContainer>
  if (wrongContext) return <PageContainer><Alert severity="warning">Esta solicitação pertence a outro condomínio.</Alert><Button sx={{ mt: 2 }} onClick={() => navigate(returnPath)}>Voltar para solicitações</Button></PageContainer>
  if (!details) return null

  const unit = details.targetUnit && `${details.targetUnit.block ? `Bloco ${details.targetUnit.block} · ` : ''}${details.targetUnit.identifier}`
  const residentReadOnly = !managementMode && isClosedRequest(details.status)
  const residentClosurePending = !managementMode && details.status === 'WaitingForResidentClosure'
  const canViewInternal = canViewInternalRequestDetails(
    managementMode, isManager, details.condominiumId, expectedCondominiumId)
  return (
    <PageContainer>
      <Button startIcon={<ArrowBackRoundedIcon />} color="inherit" onClick={() => navigate(returnPath)} sx={{ mb: 2 }}>Voltar</Button>
      {managementMode && <Button sx={{ ml: 1, mb: 2 }} variant="outlined" onClick={() => navigate(`/management/assistant?requestId=${details.id}`)}>Consultar assistente</Button>}
      {(location.state as { created?: boolean } | null)?.created && <Alert severity="success" sx={{ mb: 2 }}>Solicitação aberta com sucesso.</Alert>}
      {actionFeedback && <Alert severity="success" sx={{ mb: 2 }}>{actionFeedback}</Alert>}
      {residentReadOnly && <Alert severity="info" sx={{ mb: 2 }}>Esta solicitação está encerrada e disponível somente para consulta.</Alert>}
      {managementMode && details.status === 'WaitingForResidentClosure' && <Alert severity="warning" sx={{ mb: 2 }}>A administração concluiu este atendimento e aguarda a confirmação do morador.</Alert>}
      {managementMode && details.hasUnreadResidentReply && <Alert severity="warning" sx={{ mb: 2 }}>Morador respondeu — requer andamento.</Alert>}
      {managementMode && <ResidentUpdateAcknowledgement requestId={details.id} visible={Boolean(details.hasUnreadResidentUpdate)} onAcknowledged={() => setDetails(current => current ? { ...current, hasUnreadResidentUpdate: false } : current)} />}
      <Grid container spacing={3}>
        <Grid size={{ xs: 12, lg: 8 }}>
          <Card elevation={0}><CardContent sx={{ p: { xs: 2.5, sm: 4 } }}>
            <Stack direction="row" flexWrap="wrap" gap={1} mb={2}><RequestStatusChip status={details.status} /><RequestPriorityChip priority={details.priority} /></Stack>
            <Typography color="primary.main" fontWeight={800}>Atendimento #{formatRequestProtocol(details.id, details.protocol)}</Typography>
            <Typography variant="h1" mt={.5}>{details.title}</Typography>
            <Typography color="text.secondary" mt={1}>{details.category.name} · aberta em {formatDateTime(details.createdAt)}</Typography>
            <Divider sx={{ my: 3 }} />
            <Typography variant="h3" mb={1}>Descrição</Typography><Typography sx={{ whiteSpace: 'pre-wrap', overflowWrap: 'anywhere' }}>{details.description}</Typography>
            {unit && <><Divider sx={{ my: 3 }} /><Typography variant="h3" mb={1}>Unidade relacionada</Typography><Typography>{unit}</Typography></>}
          </CardContent></Card>
          {managementMode && details.residentSummary
            && <ResidentSummaryCard resident={details.residentSummary} />}
          {canViewInternal && <RequestManagementActions requestId={details.id} status={details.status} priority={details.priority} agendaReminder={details.agendaReminder} onUpdated={load} />}
          {!managementMode && details.residentClosureProposal && <ResidentClosurePanel requestId={details.id} proposal={details.residentClosureProposal} onUpdated={async feedback => { if (feedback) setActionFeedback(feedback); await load() }} />}
          <Card elevation={0} sx={{ mt: 3 }}><CardContent sx={{ p: { xs: 2.5, sm: 4 } }}><Typography variant="h2" mb={.5}>Atualizações</Typography><Typography color="text.secondary" mb={3}>{residentReadOnly ? 'Consulte o histórico de mensagens do atendimento.' : 'Registre novas informações e acompanhe o atendimento.'}</Typography><RequestConversation requestId={details.id} status={details.status} messages={messages} residentSummary={details.aiAnalysis?.description} readOnly={residentReadOnly || residentClosurePending || (!managementMode && Boolean(details.residentReplyRequirement))} onMessageCreated={(message) => setMessages((current) => [...current, message])} /></CardContent></Card>
          {canViewInternal && <RequestAiAssistant analysis={details.aiAnalysis} />}
          {canViewInternal && <OriginalReportAccordion key={details.id} requestId={details.id} report={details.originalReport} messages={messages} authorId={details.author.id} portalDescription={details.description} requestCreatedAt={details.createdAt} />}
          {!managementMode && details.residentReplyRequirement && <ResidentReplyPanel requestId={details.id} requirement={details.residentReplyRequirement} onSent={load} />}
          <RequestAttachments requestId={details.id} readOnly={residentReadOnly || residentClosurePending || Boolean(!managementMode && details.residentReplyRequirement)} />
        </Grid>
        <Grid size={{ xs: 12, lg: 4 }}><Card elevation={0}><CardContent sx={{ p: { xs: 2.5, sm: 3 } }}><Typography variant="h2" mb={3}>Timeline</Typography><RequestTimeline history={details.statusHistory} messages={messages} /></CardContent></Card></Grid>
      </Grid>
    </PageContainer>
  )
}

export function ManagementRequestDetailsPage() {
  return <RequestDetailsPage managementMode />
}
