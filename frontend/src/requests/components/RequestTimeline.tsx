import { WhatsAppDeliveryIndicator } from './WhatsAppDeliveryIndicator'
import { Box, Stack, Typography } from '@mui/material'
import { formatDateTime, statusPresentation } from '../presentation'
import type { ProviderPaymentRequestItem, RequestInternalNoteSummary, RequestMessage, ServiceProviderHistoryItem, StatusHistoryItem } from '../types'
import { newestStatusHistoryFirst } from '../requestUpdates'

function historyTitle(item: StatusHistoryItem) {
  if (item.newStatus === 'WaitingForResidentClosure') return 'Concluído pela administração — aguardando confirmação'
  if (item.previousStatus === 'WaitingForResidentClosure' && item.newStatus === 'Resolved') return item.reason?.includes('prazo') ? 'Atendimento finalizado automaticamente' : 'Atendimento finalizado pelo morador'
  if (item.previousStatus === 'WaitingForResidentClosure' && item.newStatus === 'InProgress') return 'Atendimento voltou para Em andamento'
  return item.previousStatus === null ? 'Solicitação aberta' : `Status alterado para ${statusPresentation[item.newStatus].label}`
}

type Entry =
  | { kind: 'status'; item: StatusHistoryItem; createdAt: string; id: string }
  | { kind: 'message'; item: RequestMessage; createdAt: string; id: string }
  | { kind: 'internal-note'; item: RequestInternalNoteSummary; createdAt: string; id: string }
  | { kind: 'provider'; item: ServiceProviderHistoryItem; createdAt: string; id: string }
  | { kind: 'provider-payment'; item: ProviderPaymentRequestItem; createdAt: string; id: string }

export function RequestTimeline({ history, messages = [], internalNotes = [], serviceProviderHistory = [], providerPaymentRequests = [] }: { history: StatusHistoryItem[]; messages?: RequestMessage[]; internalNotes?: RequestInternalNoteSummary[] | null; serviceProviderHistory?: ServiceProviderHistoryItem[]; providerPaymentRequests?: ProviderPaymentRequestItem[] }) {
  const correlatedAnswerIds = new Set(history.flatMap(item => item.answerMessageId ? [item.answerMessageId] : []))
  const entries: Entry[] = [
    ...newestStatusHistoryFirst(history).map(item => ({ kind: 'status' as const, item, createdAt: item.createdAt, id: item.id })),
    ...messages.filter(item => !correlatedAnswerIds.has(item.id) && !item.isAdministrativeEvent).map(item => ({ kind: 'message' as const, item, createdAt: item.createdAt, id: item.id })),
    ...(internalNotes ?? []).map(item => ({ kind: 'internal-note' as const, item, createdAt: item.createdAt, id: item.id })),
    ...serviceProviderHistory.map(item => ({ kind: 'provider' as const, item, createdAt: item.createdAt, id: item.id })),
    ...providerPaymentRequests.map(item => ({ kind: 'provider-payment' as const, item, createdAt: item.createdAt, id: item.id })),
  ].sort((left, right) => right.createdAt.localeCompare(left.createdAt) || right.id.localeCompare(left.id))

  return <Stack component="ol" spacing={0} sx={{ listStyle: 'none', p: 0, m: 0 }}>{entries.map((entry, index) => <Box component="li" key={`${entry.kind}-${entry.id}`} display="grid" gridTemplateColumns="24px 1fr" gap={1.5}><Box display="flex" flexDirection="column" alignItems="center"><Box width={10} height={10} borderRadius="50%" bgcolor="primary.main" mt={.75} />{index < entries.length - 1 && <Box width="2px" flex={1} minHeight={46} bgcolor="divider" />}</Box><Box pb={index < entries.length - 1 ? 2.5 : 0}>{entry.kind === 'provider-payment' ? <><Typography fontWeight={700}>Pagamento solicitado à administradora</Typography><Typography mt={.75}>{entry.item.providerName} · {new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(entry.item.value)}</Typography><Typography color="text.secondary" fontSize=".82rem">por {entry.item.createdByFullName} · {formatDateTime(entry.item.createdAt)} · {entry.item.friendlyIdentifier}</Typography></> : entry.kind === 'provider' ? <><Typography fontWeight={700}>{entry.item.eventType === 'Linked' ? 'Prestador vinculado' : entry.item.eventType === 'Changed' ? 'Prestador alterado' : 'Prestador removido'}</Typography><Typography color="text.secondary" fontSize=".82rem">{entry.item.changedByFullName} · {formatDateTime(entry.item.createdAt)}</Typography><Typography mt={.75}>{entry.item.eventType === 'Changed' ? `${entry.item.previousName} — ${entry.item.previousSpecialty} → ${entry.item.providerName} — ${entry.item.providerSpecialty}` : `${entry.item.providerName ?? entry.item.previousName} — ${entry.item.providerSpecialty ?? entry.item.previousSpecialty}`}</Typography></> : entry.kind === 'internal-note' ? <><Typography fontWeight={700}>🔒 Nota interna</Typography><Typography color="text.secondary" fontSize=".82rem">{entry.item.author.fullName} · {formatDateTime(entry.item.createdAt)}{entry.item.updatedAt ? ' · Editada' : ''}</Typography><Typography mt={.75} sx={{ whiteSpace: 'pre-wrap', overflowWrap: 'anywhere' }}>{entry.item.content}</Typography></> : entry.kind === 'status' ? <><Typography fontWeight={700}>{historyTitle(entry.item)}</Typography><Typography color="text.secondary" fontSize=".82rem">{entry.item.changedByFullName} · {formatDateTime(entry.item.createdAt)} <WhatsAppDeliveryIndicator delivery={entry.item.whatsAppDelivery} /></Typography>{entry.item.answerMessageId ? <Typography mt={.75} sx={{ whiteSpace: 'pre-wrap', overflowWrap: 'anywhere' }}>Resposta recebida do morador: {messages.find(message => message.id === entry.item.answerMessageId)?.content ?? 'conteúdo indisponível.'}</Typography> : entry.item.reason && <Typography mt={.75}>{entry.item.reason}</Typography>}</> : <><Typography fontWeight={700}>{entry.item.channel === 'System' ? 'Evento do sistema' : entry.item.author.isManager ? 'Mensagem da gestão' : 'Atualização do morador'}</Typography><Typography color="text.secondary" fontSize=".82rem">{entry.item.author.fullName} · {formatDateTime(entry.item.createdAt)} <WhatsAppDeliveryIndicator delivery={entry.item.whatsAppDelivery} /></Typography><Typography mt={.75} sx={{ whiteSpace: 'pre-wrap', overflowWrap: 'anywhere' }}>{entry.item.content}</Typography></>}</Box></Box>)}</Stack>
}
