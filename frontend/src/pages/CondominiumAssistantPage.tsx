import { useCallback, useEffect, useRef, useState } from "react";
import { streamAssistant } from "../assistant/streamAssistant";
import { Alert, Box, Button, CircularProgress, Dialog, DialogActions, DialogContent, DialogTitle, Drawer, IconButton, Menu, MenuItem, Skeleton, Stack, TextField, Typography } from "@mui/material";
import HistoryRoundedIcon from "@mui/icons-material/HistoryRounded";
import DeleteOutlineRoundedIcon from "@mui/icons-material/DeleteOutlineRounded";
import MoreVertRoundedIcon from "@mui/icons-material/MoreVertRounded";
import StopRoundedIcon from "@mui/icons-material/StopRounded";
import { useSearchParams } from "react-router-dom";
import { PageContainer } from "../components/PageContainer";
import { PageHeader } from "../components/PageHeader";
import { useManagementContext } from "../management/ManagementContext";
import { getErrorMessage } from "../services/api";
import { AssistantSources } from "../assistant/AssistantSources";
import { deleteConversation, downloadDocument, getConversation, listConversations, listDocuments, removeRequestContext, type AssistantConversation, type AssistantMessage } from "../assistant/api";
export { CondominiumDocumentsPage } from './CondominiumDocumentsPage';

const suggestions = [["Operação", "O que precisa da minha atenção?"], ["Atendimentos", "Quais atendimentos estão aguardando ação?"], ["Pessoas", "Encontre um morador"], ["Agenda", "O que tenho para hoje?"], ["Prestadores", "Encontre um prestador"], ["Documentos", "Consulte o regimento"]];

export function CondominiumAssistantPage() {
  const { activeCondominiumId, activeCondominium } = useManagementContext();
  const [params] = useSearchParams();
  const initialRequestId = params.get("requestId") ?? undefined;
  const [conversation, setConversation] = useState<AssistantConversation | null>(null);
  const [pendingRequestId, setPendingRequestId] = useState(initialRequestId);
  const [requestContext, setRequestContext] = useState<{ id: string; title: string } | null>(null);
  const [conversations, setConversations] = useState<AssistantConversation[]>([]);
  const [historyPage, setHistoryPage] = useState(1);
  const [hasMore, setHasMore] = useState(false);
  const [historySearch, setHistorySearch] = useState("");
  const [messages, setMessages] = useState<AssistantMessage[]>([]);
  const [question, setQuestion] = useState("");
  const [sending, setSending] = useState(false);
  const [opening, setOpening] = useState(false);
  const [historyLoading, setHistoryLoading] = useState(false);
  const [error, setError] = useState("");
  const [historyError, setHistoryError] = useState("");
  const [conversationError, setConversationError] = useState("");
  const [documentState, setDocumentState] = useState<"none" | "processing" | "ready" | "unknown">("unknown");
  const [drawer, setDrawer] = useState(false);
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<{ id: string; title: string } | null>(null);
  const [menuAnchor, setMenuAnchor] = useState<HTMLElement | null>(null);
  const [menuTarget, setMenuTarget] = useState<AssistantConversation | null>(null);
  const [searching, setSearching] = useState(false);
  const endRef = useRef<HTMLDivElement>(null);
  const composerRef = useRef<HTMLInputElement | HTMLTextAreaElement>(null);
  const streamAbortRef = useRef<AbortController | null>(null);
  const documentLoadRef = useRef(0);

  useEffect(() => () => streamAbortRef.current?.abort(), []);
  const loadHistory = useCallback(async (page = 1, append = false, search = "") => {
    if (!activeCondominiumId) return;
    setHistoryLoading(true);
    try {
      const result = await listConversations(activeCondominiumId, page, search);
      setConversations(current => append ? [...current, ...result.items] : result.items);
      setHistoryPage(page); setHasMore(result.hasMore); setHistoryError("");
    } catch (loadError) { setHistoryError(getErrorMessage(loadError)); }
    finally { setHistoryLoading(false); }
  }, [activeCondominiumId]);

  useEffect(() => {
    setConversation(null); setMessages([]); setPendingRequestId(initialRequestId); setRequestContext(null); setConversationError(""); void loadHistory();
    const loadId = ++documentLoadRef.current;
    if (!activeCondominiumId) { setDocumentState("unknown"); return; }
    void listDocuments(activeCondominiumId).then(documents => {
      if (loadId !== documentLoadRef.current) return;
      if (documents.length === 0) setDocumentState("none");
      else if (documents.some(document => ["Pending", "Processing"].includes(document.processingStatus))) setDocumentState("processing");
      else if (documents.some(document => document.processingStatus === "Ready" && document.isActive)) setDocumentState("ready");
      else setDocumentState("none");
    }).catch(() => { if (loadId === documentLoadRef.current) setDocumentState("unknown"); });
  }, [activeCondominiumId, initialRequestId, loadHistory]);
  useEffect(() => { if (typeof endRef.current?.scrollIntoView === "function") endRef.current.scrollIntoView({ behavior: "smooth" }); }, [messages, sending]);

  const open = async (item: AssistantConversation) => {
    if (!activeCondominiumId) return;
    streamAbortRef.current?.abort(); setOpening(true); setDrawer(false); setConversationError("");
    try {
      const details = await getConversation(activeCondominiumId, item.id);
      setConversation(details.conversation); setMessages(details.messages); setRequestContext(details.requestContext); setPendingRequestId(undefined);
      if (details.contextUnavailable) setConversationError("O atendimento associado a esta conversa não está mais disponível. A conversa pode continuar sem esse contexto.");
    } catch (openError) { setConversationError(getErrorMessage(openError)); }
    finally { setOpening(false); }
  };
  const fresh = () => { streamAbortRef.current?.abort(); setConversation(null); setMessages([]); setRequestContext(null); setPendingRequestId(undefined); setQuestion(""); setError(""); setConversationError(""); setDrawer(false); };
  const send = async () => {
    if (!activeCondominiumId || !question.trim() || sending) return;
    const value = question.trim(); const answerId = `answer-${Date.now()}`; let receivedToken = false;
    setQuestion(""); setSending(true); setSearching(true); setError("");
    setMessages(x => [...x, { id: `pending-${Date.now()}`, role: "User", content: value, createdAt: new Date().toISOString(), sources: [], operationalReferences: [] }]);
    streamAbortRef.current?.abort(); const controller = new AbortController(); streamAbortRef.current = controller;
    const path = conversation ? `/condominiums/${activeCondominiumId}/assistant/conversations/${conversation.id}/messages` : `/condominiums/${activeCondominiumId}/assistant/messages`;
    const body = conversation ? { question: value } : { question: value, requestId: pendingRequestId };
    const upsertAnswer = (content: string, sources: AssistantMessage["sources"], operationalReferences: AssistantMessage["operationalReferences"] = []) => setMessages(x => x.some(m => m.id === answerId) ? x.map(m => m.id === answerId ? { ...m, content, sources, operationalReferences } : m) : [...x, { id: answerId, role: "Assistant", content, createdAt: new Date().toISOString(), sources, operationalReferences }]);
    try {
      await streamAssistant(path, body, {
        onSources: () => setSearching(false),
        onToken: delta => { receivedToken = true; setMessages(x => x.some(m => m.id === answerId) ? x.map(m => m.id === answerId ? { ...m, content: m.content + delta } : m) : [...x, { id: answerId, role: "Assistant", content: delta, createdAt: new Date().toISOString(), sources: [], operationalReferences: [] }]); },
        onDone: result => { if (!conversation && result.conversation) { setConversation(result.conversation); setPendingRequestId(undefined); } upsertAnswer(result.answer, result.sources, result.operationalReferences); void loadHistory(1, false, historySearch); },
        onError: message => { if (!receivedToken) setMessages(x => x.filter(m => m.id !== answerId)); setError(receivedToken ? `${message} A resposta parcial permanece disponível.` : message); },
      }, controller.signal);
    } finally { setSending(false); setSearching(false); }
  };
  const remove = async () => {
    if (!activeCondominiumId || !conversation) return;
    try { await removeRequestContext(activeCondominiumId, conversation.id); setConversation({ ...conversation, requestId: null }); setRequestContext(null); }
    catch (removeError) { setConversationError(getErrorMessage(removeError)); }
  };
  const removeConversation = async () => {
    if (!activeCondominiumId || !deleteTarget) return;
    try { await deleteConversation(activeCondominiumId, deleteTarget.id); setDeleteOpen(false); setDeleteTarget(null); fresh(); await loadHistory(1, false, historySearch); }
    catch (deleteError) { setError(getErrorMessage(deleteError)); }
  };
  if (!activeCondominiumId) return <PageContainer><Alert severity="info"><Typography fontWeight={800}>Selecione um condomínio</Typography>Este módulo trabalha com um condomínio por vez. Escolha um condomínio no seletor acima para continuar.</Alert></PageContainer>;
  const requestLabel = conversation?.requestId ?? pendingRequestId;
  const contextLabel = requestLabel ? `Atendimento #${requestLabel.slice(0, 8)}${requestContext ? ` · ${requestContext.title}` : ""}` : null;
  const history = <Stack data-testid="assistant-history" sx={{ width: { xs: 300, md: 264 }, p: { xs: 1.5, md: 2 }, height: "100%", minHeight: 0, overflowY: "auto" }} gap={1}>
    <Stack direction="row" alignItems="center" justifyContent="space-between" gap={1}><Typography variant="overline" color="text.secondary" fontWeight={800}>Conversas</Typography><Button size="small" onClick={fresh}>+ Nova conversa</Button></Stack>
    <TextField size="small" label="Buscar conversas" placeholder="Título ou contexto" value={historySearch} onChange={event => { const value = event.target.value; setHistorySearch(value); void loadHistory(1, false, value); }} />
    {historyError && <Alert severity="error">{historyError}</Alert>}
    {historyLoading && conversations.length === 0 ? <Skeleton height={120} /> : conversations.length === 0 ? <Typography color="text.secondary" p={1}>Nenhuma conversa anterior.</Typography> : conversations.map(item => <Stack key={item.id} direction="row" alignItems="stretch" gap={.25} sx={{ borderBottom: "1px solid", borderColor: "divider" }}>
      <Button data-testid={`assistant-conversation-${item.id}`} color="inherit" onClick={() => void open(item)} sx={{ flex: 1, minWidth: 0, display: "block", textAlign: "left", p: 1, borderRadius: 1, bgcolor: conversation?.id === item.id ? "action.selected" : undefined }}><Typography fontWeight={700} noWrap>{item.title}</Typography><Typography variant="caption" color="text.secondary" noWrap display="block">{item.requestId ? `Atendimento #${item.requestId.slice(0, 8)} · ` : ""}{new Date(item.updatedAt).toLocaleDateString("pt-BR")}</Typography></Button>
      <IconButton size="small" aria-label="Ações da conversa" onClick={event => { event.stopPropagation(); setMenuAnchor(event.currentTarget); setMenuTarget(item); }} sx={{ alignSelf: "center" }}><MoreVertRoundedIcon fontSize="small" /></IconButton>
    </Stack>)}
    {hasMore && <Button disabled={historyLoading} onClick={() => void loadHistory(historyPage + 1, true, historySearch)}>Carregar mais</Button>}
  </Stack>;
  return <PageContainer>
    <PageHeader title="Assistente" description="Pergunte sobre seu condomínio e sua operação." actions={<><Button variant="outlined" startIcon={<HistoryRoundedIcon />} sx={{ display: { xs: "inline-flex", md: "none" } }} onClick={() => setDrawer(true)}>Histórico</Button><Button variant="contained" onClick={fresh}>Nova conversa</Button>{conversation && <IconButton aria-label="Excluir conversa" onClick={() => { setDeleteTarget({ id: conversation.id, title: conversation.title }); setDeleteOpen(true); }}><DeleteOutlineRoundedIcon /></IconButton>}</>} />
    <Stack gap={1.5}>
      <Stack direction="row" alignItems="center" justifyContent="space-between" gap={1} flexWrap="wrap"><Typography variant="caption" color="text.secondary" fontWeight={800}>Consultando · {activeCondominium?.name ?? "condomínio atual"}</Typography>{documentState === "processing" && <Typography variant="caption" color="text.secondary" aria-live="polite">Alguns documentos ainda estão sendo preparados para consulta.</Typography>}</Stack>
      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", md: "264px minmax(0, 1fr)" }, gap: { xs: 1.5, md: 2.5 }, alignItems: "stretch", height: { md: "calc(100dvh - 13rem)" }, minHeight: { md: 0 } }}>
        <Box sx={{ display: { xs: "none", md: "flex" }, minHeight: 0, borderRight: "1px solid", borderColor: "divider" }}>{history}</Box>
        <Box sx={{ minWidth: 0, minHeight: 0, display: "flex", flexDirection: "column" }}>
          {contextLabel && <Box sx={{ borderBottom: "1px solid", borderColor: "divider", pb: 1, mb: 1.5 }}><Stack direction="row" alignItems="center" justifyContent="space-between" gap={1}><Typography variant="caption" color="text.secondary"><strong>Contexto</strong> · {contextLabel}</Typography>{conversation && <Button size="small" onClick={() => void remove()}>Remover</Button>}</Stack></Box>}
          {(conversationError || error) && <Alert severity="warning" sx={{ mb: 1.5 }}>{conversationError || error}</Alert>}
          <Stack data-testid="assistant-chat" gap={2} minHeight={0} overflow="auto" sx={{ minWidth: 0, overflowWrap: "anywhere", flex: 1, pr: { md: 2 } }}>
            {opening ? <Skeleton height={240} /> : messages.length === 0 ? <Stack gap={2} sx={{ maxWidth: 820, width: "100%", mx: "auto", py: { xs: 2, md: 5 } }}><Box><Typography variant="h2" sx={{ fontSize: { xs: "1.45rem", md: "1.8rem" } }}>O que você precisa saber sobre {activeCondominium?.name ?? "o condomínio"}?</Typography><Typography color="text.secondary" mt={.75}>Posso consultar a operação e os documentos do condomínio.</Typography></Box>{documentState === "none" && <Alert severity="info">Ainda não há documentos disponíveis para consulta. As consultas operacionais continuam disponíveis.</Alert>}<Stack direction="row" flexWrap="wrap" gap={1}>{suggestions.map(([category, label]) => <Button key={label} variant="outlined" size="small" onClick={() => { setQuestion(label); window.setTimeout(() => composerRef.current?.focus(), 0); }} sx={{ justifyContent: "flex-start", textTransform: "none", borderColor: "divider" }}><Box component="span" sx={{ textAlign: "left" }}><Typography component="span" variant="caption" color="text.secondary" display="block">{category}</Typography>{label}</Box></Button>)}</Stack></Stack> : messages.map(message => <Stack key={message.id} alignItems={message.role === "User" ? "flex-end" : "stretch"} sx={{ maxWidth: 900, width: "100%", mx: "auto" }}><Typography variant="caption" color="text.secondary" fontWeight={800} mb={.35}>{message.role === "User" ? "Você" : "Assistente"}</Typography><Typography sx={{ color: "text.primary", px: message.role === "User" ? 1.25 : 0, py: message.role === "User" ? .9 : 0, borderRadius: 1.5, bgcolor: message.role === "User" ? "action.hover" : "transparent", maxWidth: message.role === "User" ? "min(85%, 620px)" : "100%", whiteSpace: "pre-wrap" }}>{message.content}</Typography>{(message.sources.length > 0 || (message.operationalReferences?.length ?? 0) > 0) && <AssistantSources sources={message.sources} operationalReferences={message.operationalReferences} onDownload={async (documentId, documentName) => { setError(""); try { await downloadDocument(activeCondominiumId, documentId, documentName); } catch (downloadError) { setError(getErrorMessage(downloadError)); } }} />}</Stack>)}
            {sending && <Stack direction="row" gap={1} alignItems="center" aria-live="polite"><CircularProgress size={18} /><Typography color="text.secondary">{searching ? "Consultando informações…" : "Preparando resposta…"}</Typography></Stack>}
            <div ref={endRef} />
          </Stack>
          <Stack direction={{ xs: "column", sm: "row" }} gap={1} mt={2} sx={{ maxWidth: 900, width: "100%", mx: "auto" }}><TextField fullWidth inputRef={composerRef} label="Pergunte ao assistente" placeholder="Pergunte sobre o condomínio…" value={question} onChange={event => setQuestion(event.target.value)} multiline maxRows={5} onKeyDown={event => { if (event.key === "Enter" && !event.shiftKey) { event.preventDefault(); if (!sending && !opening && question.trim()) void send(); } }} />{sending ? <Button variant="outlined" color="inherit" startIcon={<StopRoundedIcon />} onClick={() => streamAbortRef.current?.abort()} aria-label="Parar geração">Parar</Button> : <Button variant="contained" disabled={opening} onClick={() => void send()}>Enviar</Button>}</Stack>
        </Box>
      </Box>
    </Stack>
    <Drawer open={drawer} onClose={() => setDrawer(false)}>{history}</Drawer>
    <Menu anchorEl={menuAnchor} open={Boolean(menuAnchor)} onClose={() => { setMenuAnchor(null); setMenuTarget(null); }}><MenuItem onClick={() => { if (menuTarget) { setDeleteTarget({ id: menuTarget.id, title: menuTarget.title }); setDeleteOpen(true); } setMenuAnchor(null); setMenuTarget(null); }}>Excluir conversa</MenuItem></Menu>
    <Dialog open={deleteOpen} onClose={() => setDeleteOpen(false)}><DialogTitle>Excluir conversa?</DialogTitle><DialogContent>{deleteTarget?.title ? `A conversa “${deleteTarget.title}” e suas mensagens serão excluídas.` : "As mensagens desta conversa serão excluídas."}</DialogContent><DialogActions><Button onClick={() => setDeleteOpen(false)}>Cancelar</Button><Button color="error" onClick={() => void removeConversation()}>Excluir</Button></DialogActions></Dialog>
  </PageContainer>;
}
