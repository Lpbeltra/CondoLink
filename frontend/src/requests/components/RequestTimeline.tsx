import { Box, Divider, Stack, Typography } from "@mui/material";
import { WhatsAppDeliveryIndicator } from "./WhatsAppDeliveryIndicator";
import { formatDateTime, statusPresentation } from "../presentation";
import type {
  ProviderPaymentRequestItem,
  RequestMessage,
  ServiceProviderHistoryItem,
  StatusHistoryItem,
} from "../types";
import { getUpdateMarkerColor } from "../requestUpdates";

function historyTitle(item: StatusHistoryItem) {
  if (item.newStatus === "WaitingForResidentClosure") return "Concluído pela administração — aguardando confirmação";
  if (item.previousStatus === "WaitingForResidentClosure" && item.newStatus === "Resolved") return item.reason?.includes("prazo") ? "Atendimento finalizado automaticamente" : "Atendimento finalizado pelo morador";
  if (item.previousStatus === "WaitingForResidentClosure" && item.newStatus === "InProgress") return "Atendimento voltou para Em andamento";
  return item.previousStatus === null ? "Solicitação aberta" : `Status alterado para ${statusPresentation[item.newStatus].label}`;
}

type Entry =
  | { kind: "message"; item: RequestMessage; createdAt: string; id: string }
  | { kind: "system"; item: RequestMessage; createdAt: string; id: string }
  | { kind: "status"; item: StatusHistoryItem; createdAt: string; id: string }
  | { kind: "provider"; item: ServiceProviderHistoryItem; createdAt: string; id: string }
  | { kind: "provider-payment"; item: ProviderPaymentRequestItem; createdAt: string; id: string };

export function combineRequestHistory(
  history: StatusHistoryItem[],
  messages: RequestMessage[] = [],
  serviceProviderHistory: ServiceProviderHistoryItem[] = [],
  providerPaymentRequests: ProviderPaymentRequestItem[] = [],
) {
  const entries: Entry[] = [
    ...history.map((item) => ({ kind: "status" as const, item, createdAt: item.createdAt, id: item.id })),
    ...messages.filter((item) => !item.isAdministrativeEvent && ["Portal", "WhatsApp", "WhatsAppResidentUpdate"].includes(item.channel ?? "Portal")).map((item) => ({ kind: "message" as const, item, createdAt: item.createdAt, id: item.id })),
    ...messages.filter((item) => item.isAdministrativeEvent || item.channel === "System").map((item) => ({ kind: "system" as const, item, createdAt: item.createdAt, id: item.id })),
    ...serviceProviderHistory.map((item) => ({ kind: "provider" as const, item, createdAt: item.createdAt, id: item.id })),
    ...providerPaymentRequests.map((item) => ({ kind: "provider-payment" as const, item, createdAt: item.createdAt, id: item.id })),
  ];
  return entries.sort((left, right) => left.createdAt.localeCompare(right.createdAt)
    || left.kind.localeCompare(right.kind) || left.id.localeCompare(right.id));
}

function Event({ children, timestamp }: { children: React.ReactNode; timestamp: string }) {
  return <Box component="article" sx={{ py: 1.25 }}><Divider /><Typography color="text.secondary" fontSize=".72rem" mt={1}>{formatDateTime(timestamp)}</Typography><Box mt={.5}>{children}</Box></Box>;
}

export function RequestHistory({ history, messages = [], serviceProviderHistory = [], providerPaymentRequests = [] }: { history: StatusHistoryItem[]; messages?: RequestMessage[]; serviceProviderHistory?: ServiceProviderHistoryItem[]; providerPaymentRequests?: ProviderPaymentRequestItem[] }) {
  const entries = combineRequestHistory(history, messages, serviceProviderHistory, providerPaymentRequests);
  if (entries.length === 0) return <Typography color="text.secondary">Ainda não há histórico neste atendimento.</Typography>;

  return <Stack component="ol" spacing={0} sx={{ listStyle: "none", p: 0, m: 0 }}>
    {entries.map((entry) => <Box component="li" key={`${entry.kind}-${entry.id}`}>
      {entry.kind === "message" ? <Box component="article" aria-label={`Mensagem ${entry.item.author.isManager ? "da gestão" : "do morador"}`} sx={{ borderLeft: "2px solid", borderLeftColor: getUpdateMarkerColor(entry.item), borderBottom: "1px solid", borderBottomColor: "divider", p: { xs: 1.25, sm: 1.5 }, minWidth: 0 }}><Typography fontWeight={750} fontSize=".8rem">{entry.item.author.isManager ? "Gestão" : "Morador"} · {entry.item.author.fullName}</Typography><Typography sx={{ whiteSpace: "pre-wrap", overflowWrap: "anywhere", lineHeight: 1.7, my: 1 }}>{entry.item.content}</Typography><Typography color="text.secondary" fontSize=".72rem">{formatDateTime(entry.item.createdAt)} <WhatsAppDeliveryIndicator delivery={entry.item.whatsAppDelivery} /></Typography></Box>
        : entry.kind === "status" ? <Event timestamp={entry.createdAt}><Typography fontWeight={700}>{historyTitle(entry.item)}</Typography><Typography color="text.secondary" fontSize=".82rem">{entry.item.changedByFullName} <WhatsAppDeliveryIndicator delivery={entry.item.whatsAppDelivery} /></Typography>{entry.item.reason && <Typography mt={.5}>{entry.item.reason}</Typography>}</Event>
        : entry.kind === "provider" ? <Event timestamp={entry.createdAt}><Typography fontWeight={700}>{entry.item.eventType === "Linked" ? "Prestador vinculado" : entry.item.eventType === "Changed" ? "Prestador alterado" : "Prestador removido"}</Typography><Typography color="text.secondary" fontSize=".82rem">{entry.item.changedByFullName}</Typography><Typography mt={.5}>{entry.item.eventType === "Changed" ? `${entry.item.previousName} — ${entry.item.previousSpecialty} → ${entry.item.providerName} — ${entry.item.providerSpecialty}` : `${entry.item.providerName ?? entry.item.previousName} — ${entry.item.providerSpecialty ?? entry.item.previousSpecialty}`}</Typography></Event>
        : entry.kind === "provider-payment" ? <Event timestamp={entry.createdAt}><Typography fontWeight={700}>Pagamento solicitado à administradora</Typography><Typography color="text.secondary">{entry.item.providerName} · {new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" }).format(entry.item.value)} · {entry.item.friendlyIdentifier}</Typography></Event>
        : <Event timestamp={entry.createdAt}><Typography fontWeight={700}>Evento do sistema</Typography><Typography color="text.secondary">{entry.item.content}</Typography></Event>}
    </Box>)}
  </Stack>;
}

/** @deprecated Use RequestHistory. */
export const RequestTimeline = RequestHistory;
