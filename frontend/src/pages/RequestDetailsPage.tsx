import { useCallback, useEffect, useRef, useState } from 'react'
import ArrowBackRoundedIcon from '@mui/icons-material/ArrowBackRounded'
import { Alert, Box, Button, Divider, Paper, Skeleton, Stack, Tab, Tabs, Typography } from '@mui/material'
import { useLocation, useNavigate, useParams } from 'react-router-dom'
import { PageContainer } from '../components/PageContainer'
import { PageHeader } from '../components/PageHeader'
import { useCondominium } from '../condominiums/CondominiumContext'
import { getRequest, listRequestMessages } from '../requests/api'
import { RequestConversation } from '../requests/components/RequestConversation'
import { RequestPriorityChip } from '../requests/components/RequestPriorityChip'
import { RequestStatusChip } from '../requests/components/RequestStatusChip'
import { RequestTimeline } from '../requests/components/RequestTimeline'
import { formatDateTime, formatRequestProtocol, formatTargetUnit, getRequestError, isClosedRequest } from '../requests/presentation'
import type { RequestDetails, RequestMessage } from '../requests/types'
import { RequestManagementActions } from '../requests/components/RequestManagementActions'
import { RequestAttachments } from '../requests/components/RequestAttachments'
import { canViewInternalRequestDetails } from '../requests/components/RequestAiAssistant'
import { RequestInternalNotes } from '../requests/components/RequestInternalNotes'
import { ResidentUpdateDialog } from '../requests/components/ResidentUpdateDialog'
import { OriginalReportAccordion } from '../requests/components/OriginalReportAccordion'
import { ResidentReplyPanel } from '../requests/components/ResidentReplyPanel'
import { ResidentClosurePanel } from '../requests/components/ResidentClosurePanel'
import { ResidentUpdateAcknowledgement } from '../requests/components/ResidentUpdateAcknowledgement'
import { useVisiblePolling } from '../hooks/useVisiblePolling'
import { ResidentSummaryCard } from '../requests/components/ResidentSummaryCard'
import { RequestServiceProviderCard } from '../requests/components/RequestServiceProviderCard'

interface RequestDetailsPageProps { managementCondominiumId?: string | null; managementMode?: boolean }
type DetailTab = 'conversation' | 'timeline' | 'data' | 'provider' | 'attachments' | 'notes'

function ContextPanel({ details, unit, managementMode, onAssistant }: { details: RequestDetails; unit: string; managementMode: boolean; onAssistant?: () => void }) {
  return <Paper component="aside" elevation={0} variant="outlined" sx={{ p: 2, position: { lg: 'sticky' }, top: { lg: 84 }, alignSelf: 'start' }}>
    <Typography variant="overline" color="text.secondary" sx={{ fontWeight: 800, letterSpacing: '.1em' }}>Contexto</Typography>
    <Stack spacing={1.5} mt={1.25}>
      <Box><Typography variant="caption" color="text.secondary">Morador</Typography><Typography fontWeight={650}>{details.residentSummary?.fullName ?? details.author.fullName}</Typography>{unit && <Typography color="text.secondary" fontSize=".8rem">{unit}</Typography>}</Box>
      <Box><Typography variant="caption" color="text.secondary">Atendimento</Typography><Stack direction="row" gap={.75} mt={.35} flexWrap="wrap"><RequestStatusChip status={details.status} /><RequestPriorityChip priority={details.priority} /></Stack></Box>
      <Box><Typography variant="caption" color="text.secondary">Categoria</Typography><Typography>{details.category.name}</Typography></Box>
      {managementMode && details.serviceProvider && <Box><Typography variant="caption" color="text.secondary">Prestador</Typography><Typography>{details.serviceProvider.name}</Typography></Box>}
      <Divider />
      <Typography color="text.secondary" fontSize=".78rem">Aberto em {formatDateTime(details.createdAt)}</Typography>
      {managementMode && onAssistant && <Button size="small" variant="outlined" onClick={onAssistant} sx={{ justifyContent: 'flex-start' }}>Consultar assistente</Button>}
    </Stack>
  </Paper>
}

export function RequestDetailsPage({ managementCondominiumId, managementMode = false }: RequestDetailsPageProps = {}) {
  const { requestId = '' } = useParams(); const navigate = useNavigate(); const location = useLocation(); const { currentCondominium, isManager } = useCondominium()
  const [details, setDetails] = useState<RequestDetails | null>(null); const [messages, setMessages] = useState<RequestMessage[]>([]); const [isLoading, setIsLoading] = useState(true); const [error, setError] = useState(''); const [actionFeedback, setActionFeedback] = useState(''); const [tab, setTab] = useState<DetailTab>('conversation')
  const loadVersion = useRef(0); const expectedCondominiumId = managementMode ? managementCondominiumId : currentCondominium?.condominium.id; const returnPath = managementMode || (location.state as { fromManagement?: boolean } | null)?.fromManagement ? '/management/requests' : '/requests'
  const load = useCallback(async (silent = false) => { const version = ++loadVersion.current; if (!managementMode && !expectedCondominiumId) { setDetails(null); setMessages([]); setIsLoading(false); return }; if (!silent) { setIsLoading(true); setError(''); setDetails(null); setMessages([]) }; try { const [request, conversation] = await Promise.all([getRequest(requestId), listRequestMessages(requestId)]); if (version !== loadVersion.current) return; setDetails(request); setMessages(conversation) } catch (requestError) { if (!silent && version === loadVersion.current) setError(getRequestError(requestError)) } finally { if (!silent && version === loadVersion.current) setIsLoading(false) } }, [expectedCondominiumId, managementMode, requestId])
  useEffect(() => { void load() }, [load]); useVisiblePolling(useCallback(() => load(true), [load]))
  if (isLoading) return <PageContainer><Skeleton variant="rounded" height={300} /></PageContainer>
  if (error) return <PageContainer><Button startIcon={<ArrowBackRoundedIcon />} onClick={() => navigate(returnPath)}>Voltar</Button><Alert severity="error" sx={{ mt: 2 }} action={<Button color="inherit" onClick={() => void load()}>Tentar novamente</Button>}>{error}</Alert></PageContainer>
  const wrongContext = !managementMode && details && details.condominiumId !== expectedCondominiumId; if (wrongContext) return <PageContainer><Alert severity="warning">Esta solicitação pertence a outro condomínio.</Alert><Button sx={{ mt: 2 }} onClick={() => navigate(returnPath)}>Voltar para solicitações</Button></PageContainer>; if (!details) return null
  const unit = formatTargetUnit(details.targetUnit); const residentReadOnly = !managementMode && isClosedRequest(details.status); const residentClosurePending = !managementMode && details.status === 'WaitingForResidentClosure'; const canViewInternal = details.canManageInternalNotes ?? canViewInternalRequestDetails(managementMode, isManager, details.condominiumId, expectedCondominiumId); const show = (name: DetailTab) => ({ display: tab === name ? 'block' : 'none' }); const tabLabel = (label: string, count?: number) => count ? `${label} (${count})` : label
  return <PageContainer maxWidth={1440}>
    <PageHeader breadcrumb={<Button size="small" startIcon={<ArrowBackRoundedIcon />} color="inherit" onClick={() => navigate(returnPath)} sx={{ alignSelf: 'flex-start', mb: .5, pl: 0 }}>Atendimento</Button>} eyebrow={`Atendimento · #${formatRequestProtocol(details.id, details.protocol)}`} title={details.title} description={`${details.author.fullName}${unit ? ` · ${unit}` : ''} · ${details.category.name}`} />
    <Stack direction="row" gap={.75} flexWrap="wrap" mb={1.5}><RequestStatusChip status={details.status} /><RequestPriorityChip priority={details.priority} /><Typography color="text.secondary" fontSize=".8rem" alignSelf="center">Aberto em {formatDateTime(details.createdAt)}</Typography></Stack>
    {(location.state as { created?: boolean } | null)?.created && <Alert severity="success" sx={{ mb: 1.5 }}>Solicitação aberta com sucesso.</Alert>}{actionFeedback && <Alert severity="success" sx={{ mb: 1.5 }}>{actionFeedback}</Alert>}{residentReadOnly && <Alert severity="info" sx={{ mb: 1.5 }}>Esta solicitação está encerrada e disponível somente para consulta.</Alert>}{managementMode && details.status === 'WaitingForResidentClosure' && <Alert severity="warning" sx={{ mb: 1.5 }}>A administração concluiu este atendimento e aguarda a confirmação do morador.</Alert>}{managementMode && details.hasUnreadResidentReply && <Alert severity="warning" sx={{ mb: 1.5 }}>Morador respondeu — requer andamento.</Alert>}{managementMode && <ResidentUpdateAcknowledgement requestId={details.id} visible={Boolean(details.hasUnreadResidentUpdate)} onAcknowledged={() => setDetails(current => current ? { ...current, hasUnreadResidentUpdate: false } : current)} />}
    <Box display="grid" gridTemplateColumns={{ xs: '1fr', lg: 'minmax(0, 1fr) 250px' }} gap={{ xs: 2, lg: 3 }}><Box minWidth={0}>
      <Tabs value={tab} onChange={(_, value: DetailTab) => setTab(value)} variant="scrollable" allowScrollButtonsMobile sx={{ borderBottom: 1, borderColor: 'divider', mb: 2 }} aria-label="Áreas do atendimento"><Tab value="conversation" label="Conversa" /><Tab value="timeline" label="Timeline" /><Tab value="data" label="Dados" /><Tab value="provider" label="Prestador" /><Tab value="attachments" label="Anexos" /><Tab value="notes" label={tabLabel('Notas', details.internalNotes?.length ?? 0)} /></Tabs>
      <Box sx={show('conversation')}><RequestConversation requestId={details.id} status={details.status} messages={messages} readOnly={residentReadOnly || residentClosurePending || (!managementMode && Boolean(details.residentReplyRequirement))} showComposer={false} onMessageCreated={message => setMessages(current => [...current, message])} />{!managementMode && !residentReadOnly && !residentClosurePending && !details.residentReplyRequirement && <ResidentUpdateDialog requestId={details.id} onSent={async message => { setMessages(current => [...current, message]); await load(true) }} />}{!managementMode && details.residentReplyRequirement && <ResidentReplyPanel requestId={details.id} requirement={details.residentReplyRequirement} onSent={load} />}</Box>
      <Box sx={show('timeline')}><RequestTimeline history={details.statusHistory} messages={messages} internalNotes={details.internalNotes} serviceProviderHistory={managementMode ? (details.serviceProviderHistory ?? []) : []} providerPaymentRequests={managementMode ? (details.providerPaymentRequests ?? []) : []} /></Box>
      <Box sx={show('data')}>{managementMode && details.residentSummary && <ResidentSummaryCard resident={details.residentSummary} />}<Box component="section" sx={{ py: 1 }}><Typography variant="h2" mb={1}>Dados do atendimento</Typography><Typography color="text.secondary" fontSize=".85rem">{details.author.fullName}{unit ? ` · ${unit}` : ''}</Typography><Divider sx={{ my: 2 }} /><Typography variant="h3" mb={.75}>Resumo</Typography><Typography sx={{ whiteSpace: 'pre-wrap', overflowWrap: 'anywhere' }}>{canViewInternal ? details.mainDescription?.trim() || details.aiAnalysis?.description?.trim() || details.description : details.description}</Typography>{canViewInternal && details.aiAnalysis?.suggestedCategory?.trim() && <Typography color="text.secondary" fontSize=".82rem" mt={1}>Sugestão da IA: {details.aiAnalysis.suggestedCategory}</Typography>}<OriginalReportAccordion key={details.id} requestId={details.id} report={details.originalReport} messages={messages} authorId={details.author.id} portalDescription={details.description} requestCreatedAt={details.createdAt} /></Box></Box>
      <Box sx={show('provider')}>{managementMode && canViewInternal && <RequestServiceProviderCard requestId={details.id} status={details.status} current={details.serviceProvider} history={details.serviceProviderHistory ?? []} paymentRequests={details.providerPaymentRequests ?? []} requestTitle={details.title} condominium={currentCondominium?.condominium.name} unit={unit} onUpdated={() => load(true)} />}</Box><Box sx={show('attachments')}><RequestAttachments requestId={details.id} readOnly={residentReadOnly || residentClosurePending || Boolean(!managementMode && details.residentReplyRequirement)} /></Box><Box sx={show('notes')}>{canViewInternal && details.canManageInternalNotes && <RequestInternalNotes requestId={details.id} onChanged={() => load(true)} />}</Box>
    </Box><Box><ContextPanel details={details} unit={unit} managementMode={managementMode} onAssistant={managementMode ? () => navigate(`/management/assistant?requestId=${details.id}`) : undefined} />{canViewInternal && <RequestManagementActions requestId={details.id} status={details.status} priority={details.priority} agendaReminder={details.agendaReminder} onUpdated={load} />}{!managementMode && details.residentClosureProposal && <ResidentClosurePanel requestId={details.id} proposal={details.residentClosureProposal} onUpdated={async feedback => { if (feedback) setActionFeedback(feedback); await load() }} />}</Box></Box>
  </PageContainer>
}
export function ManagementRequestDetailsPage() { return <RequestDetailsPage managementMode /> }
