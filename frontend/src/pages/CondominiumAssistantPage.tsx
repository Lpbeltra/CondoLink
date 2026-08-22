import { useEffect, useRef, useState } from "react";
import { streamAssistant } from "../assistant/streamAssistant";
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Drawer,
  IconButton,
  Skeleton,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import HistoryRoundedIcon from "@mui/icons-material/HistoryRounded";
import DeleteOutlineRoundedIcon from "@mui/icons-material/DeleteOutlineRounded";
import { useSearchParams } from "react-router-dom";
import { PageContainer } from "../components/PageContainer";
import { useManagementContext } from "../management/ManagementContext";
import { getErrorMessage } from "../services/api";
import {
  deleteConversation,
  downloadDocument,
  getConversation,
  listConversations,
  removeRequestContext,
  type AssistantConversation,
  type AssistantMessage,
} from "../assistant/api";
export { CondominiumDocumentsPage } from './CondominiumDocumentsPage'

export function CondominiumAssistantPage() {
  const { activeCondominiumId } = useManagementContext();
  const [params] = useSearchParams();
  const initialRequestId = params.get("requestId") ?? undefined;
  const [conversation, setConversation] =
    useState<AssistantConversation | null>(null);
  const [pendingRequestId, setPendingRequestId] = useState(initialRequestId);
  const [requestContext, setRequestContext] = useState<{
    id: string;
    title: string;
  } | null>(null);
  const [conversations, setConversations] = useState<AssistantConversation[]>(
    [],
  );
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
  const [drawer, setDrawer] = useState(false);
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [searching, setSearching] = useState(false);
  const endRef = useRef<HTMLDivElement>(null);
  const streamAbortRef = useRef<AbortController | null>(null);
  useEffect(() => () => streamAbortRef.current?.abort(), []);
  const loadHistory = async (
    page = 1,
    append = false,
    search = historySearch,
  ) => {
    if (!activeCondominiumId) return;
    setHistoryLoading(true);
    try {
      const result = await listConversations(activeCondominiumId, page, search);
      setConversations((current) =>
        append ? [...current, ...result.items] : result.items,
      );
      setHistoryPage(page);
      setHasMore(result.hasMore);
      setHistoryError("");
    } catch (loadHistoryError) {
      setHistoryError(getErrorMessage(loadHistoryError));
    } finally {
      setHistoryLoading(false);
    }
  };
  useEffect(() => {
    setConversation(null);
    setMessages([]);
    setPendingRequestId(initialRequestId);
    void loadHistory();
  }, [activeCondominiumId, initialRequestId]);
  useEffect(() => {
    if (typeof endRef.current?.scrollIntoView === "function")
      endRef.current.scrollIntoView({ behavior: "smooth" });
  }, [messages, sending]);
  const open = async (item: AssistantConversation) => {
    if (!activeCondominiumId) return;
    streamAbortRef.current?.abort();
    setOpening(true);
    setDrawer(false);
    try {
      const details = await getConversation(activeCondominiumId, item.id);
      setConversation(details.conversation);
      setMessages(details.messages);
      setRequestContext(details.requestContext);
      setPendingRequestId(undefined);
      setError(
        details.contextUnavailable
          ? "O atendimento associado a esta conversa não está mais disponível. A conversa pode continuar sem esse contexto."
          : "",
      );
    } catch (openError) {
      setError(getErrorMessage(openError));
    } finally {
      setOpening(false);
    }
  };
  const fresh = () => {
    streamAbortRef.current?.abort();
    setConversation(null);
    setMessages([]);
    setRequestContext(null);
    setPendingRequestId(undefined);
    setQuestion("");
    setError("");
    setDrawer(false);
  };
  const send = async () => {
    if (!activeCondominiumId || !question.trim() || sending) return;
    const value = question.trim();
    const answerId = `answer-${Date.now()}`;
    setQuestion("");
    setSending(true);
    setSearching(true);
    setError("");
    setMessages((x) => [
      ...x,
      {
        id: `pending-${Date.now()}`,
        role: "User",
        content: value,
        createdAt: new Date().toISOString(),
        sources: [],
      },
    ]);
    streamAbortRef.current?.abort();
    const controller = new AbortController();
    streamAbortRef.current = controller;
    const path = conversation
      ? `/condominiums/${activeCondominiumId}/assistant/conversations/${conversation.id}/messages`
      : `/condominiums/${activeCondominiumId}/assistant/messages`;
    const body = conversation
      ? { question: value }
      : { question: value, requestId: pendingRequestId };
    const upsertAnswer = (content: string, sources: AssistantMessage["sources"]) =>
      setMessages((x) =>
        x.some((m) => m.id === answerId)
          ? x.map((m) => (m.id === answerId ? { ...m, content, sources } : m))
          : [
              ...x,
              {
                id: answerId,
                role: "Assistant",
                content,
                createdAt: new Date().toISOString(),
                sources,
              },
            ],
      );
    try {
      await streamAssistant(
        path,
        body,
        {
          onSources: () => setSearching(false),
          onToken: (delta) =>
            setMessages((x) =>
              x.some((m) => m.id === answerId)
                ? x.map((m) =>
                    m.id === answerId ? { ...m, content: m.content + delta } : m,
                  )
                : [
                    ...x,
                    {
                      id: answerId,
                      role: "Assistant",
                      content: delta,
                      createdAt: new Date().toISOString(),
                      sources: [],
                    },
                  ],
            ),
          onDone: (result) => {
            if (!conversation && result.conversation) {
              setConversation(result.conversation);
              setPendingRequestId(undefined);
            }
            upsertAnswer(
              result.answer,
              result.sources.map((source) => ({
                source,
                documentCurrentlyActive: true,
              })),
            );
            void loadHistory();
          },
          onError: (message) => {
            setMessages((x) => x.filter((m) => m.id !== answerId));
            setError(message);
          },
        },
        controller.signal,
      );
    } finally {
      setSending(false);
      setSearching(false);
    }
  };
  const remove = async () => {
    if (!activeCondominiumId || !conversation) return;
    await removeRequestContext(activeCondominiumId, conversation.id);
    setConversation({ ...conversation, requestId: null });
    setRequestContext(null);
  };
  const removeConversation = async () => {
    if (!activeCondominiumId || !conversation) return;
    await deleteConversation(activeCondominiumId, conversation.id);
    setDeleteOpen(false);
    fresh();
    await loadHistory();
  };
  const history = (
    <Stack
      sx={{
        width: { xs: 300, md: 280 },
        p: 2,
        height: "100%",
        overflow: "auto",
      }}
      gap={1}
    >
      <TextField
        size="small"
        label="Buscar conversas"
        value={historySearch}
        onChange={(event) => {
          const value = event.target.value;
          setHistorySearch(value);
          void loadHistory(1, false, value);
        }}
      />
      {historyError && <Alert severity="error">{historyError}</Alert>}
      {historyLoading && conversations.length === 0 ? (
        <Skeleton height={120} />
      ) : conversations.length === 0 ? (
        <Typography color="text.secondary" p={1}>
          Nenhuma conversa anterior.
        </Typography>
      ) : (
        conversations.map((item) => (
          <Button
            key={item.id}
            color="inherit"
            onClick={() => void open(item)}
            sx={{
              display: "block",
              textAlign: "left",
              p: 1.5,
              bgcolor:
                conversation?.id === item.id ? "action.selected" : undefined,
            }}
          >
            <Typography fontWeight={800} noWrap>
              {item.title}
            </Typography>
            <Typography variant="caption" color="text.secondary">
              {item.requestId
                ? `Atendimento #${item.requestId.slice(0, 8)} · `
                : ""}
              {new Date(item.updatedAt).toLocaleString("pt-BR")}
            </Typography>
          </Button>
        ))
      )}
      {hasMore && (
        <Button
          disabled={historyLoading}
          onClick={() => void loadHistory(historyPage + 1, true)}
        >
          Carregar mais
        </Button>
      )}
    </Stack>
  );
  return (
    <PageContainer>
      <Stack gap={2}>
        <Stack direction="row" justifyContent="space-between">
          <div>
            <Typography variant="h1">Assistente do condomínio</Typography>
            <Typography color="text.secondary">
              Consulte documentos, regras e informações do condomínio.
            </Typography>
          </div>
          <Stack direction="row">
            <IconButton
              sx={{ display: { md: "none" } }}
              onClick={() => setDrawer(true)}
              aria-label="Abrir histórico"
            >
              <HistoryRoundedIcon />
            </IconButton>
            <Button onClick={fresh}>Nova conversa</Button>
            {conversation && (
              <IconButton
                onClick={() => setDeleteOpen(true)}
                aria-label="Excluir conversa"
              >
                <DeleteOutlineRoundedIcon />
              </IconButton>
            )}
          </Stack>
        </Stack>
        <Box
          sx={{
            display: "grid",
            gridTemplateColumns: { xs: "1fr", md: "280px minmax(0, 1fr)" },
            gap: 2,
          }}
        >
          <Card sx={{ display: { xs: "none", md: "block" } }}>{history}</Card>
          <Card>
            <CardContent>
              {(conversation?.requestId || pendingRequestId) && (
                <Alert
                  severity="info"
                  sx={{ mb: 2 }}
                  action={
                    conversation ? (
                      <Button onClick={() => void remove()}>
                        Remover contexto
                      </Button>
                    ) : undefined
                  }
                >
                  Contexto: Atendimento #
                  {(conversation?.requestId ?? pendingRequestId)!.slice(0, 8)}
                  {requestContext ? ` — ${requestContext.title}` : ""}
                </Alert>
              )}
              {error && (
                <Alert severity="warning" sx={{ mb: 2 }}>
                  {error}
                </Alert>
              )}
              <Stack gap={2} minHeight={360} maxHeight="60vh" overflow="auto">
                {opening ? (
                  <Skeleton height={240} />
                ) : messages.length === 0 ? (
                  <Stack gap={1}>
                    <Typography color="text.secondary">
                      Pergunte sobre documentos, regras ou informações do
                      condomínio.
                    </Typography>
                  </Stack>
                ) : (
                  messages.map((message) => (
                    <Stack
                      key={message.id}
                      alignItems={
                        message.role === "User" ? "flex-end" : "flex-start"
                      }
                    >
                      <Typography
                        sx={{
                          bgcolor:
                            message.role === "User"
                              ? "primary.main"
                              : "action.hover",
                          color:
                            message.role === "User"
                              ? "primary.contrastText"
                              : "text.primary",
                          p: 1.5,
                          borderRadius: 2,
                          maxWidth: "85%",
                          whiteSpace: "pre-wrap",
                        }}
                      >
                        {message.content}
                      </Typography>
                      {message.sources.length > 0 && (
                        <Stack mt={1} gap={0.5} maxWidth="85%">
                          <Typography variant="caption" fontWeight={800}>
                            Fontes
                          </Typography>
                          {message.sources.map(
                            ({
                              source,
                              documentExists = true,
                              documentCurrentlyActive,
                            }) =>
                              documentExists ? (
                                <Chip
                                  key={`${message.id}-${source.marker}`}
                                  clickable
                                  onClick={async () => {
                                    setError("");
                                    try {
                                      await downloadDocument(
                                        activeCondominiumId!,
                                        source.documentId,
                                        source.documentName,
                                      );
                                    } catch (downloadError) {
                                      setError(getErrorMessage(downloadError));
                                    }
                                  }}
                                  label={`${source.documentName}${source.pageNumber ? ` — pág. ${source.pageNumber}` : ""}${documentCurrentlyActive ? "" : " · documento atualmente inativo"}`}
                                />
                              ) : (
                                <Chip
                                  key={`${message.id}-${source.marker}`}
                                  label={`${source.documentName}${source.pageNumber ? ` — pág. ${source.pageNumber}` : ""} · documento removido`}
                                />
                              ),
                          )}
                        </Stack>
                      )}
                    </Stack>
                  ))
                )}
                {sending && searching && (
                  <Stack direction="row" gap={1}>
                    <CircularProgress size={18} />
                    <Typography color="text.secondary">
                      Consultando documentos…
                    </Typography>
                  </Stack>
                )}
                <div ref={endRef} />
              </Stack>
              <Stack direction={{ xs: "column", sm: "row" }} gap={1} mt={2}>
                <TextField
                  fullWidth
                  label="Pergunte ao assistente"
                  value={question}
                  onChange={(event) => setQuestion(event.target.value)}
                  multiline
                  maxRows={5}
                  onKeyDown={(event) => {
                    if (event.key === "Enter" && !event.shiftKey) {
                      event.preventDefault();
                      if (!sending && !opening && question.trim()) void send();
                    }
                  }}
                />
                <Button
                  variant="contained"
                  disabled={sending || opening}
                  onClick={() => void send()}
                >
                  Enviar
                </Button>
              </Stack>
            </CardContent>
          </Card>
        </Box>
        <Drawer open={drawer} onClose={() => setDrawer(false)}>
          {history}
        </Drawer>
        <Dialog open={deleteOpen} onClose={() => setDeleteOpen(false)}>
          <DialogTitle>Excluir conversa?</DialogTitle>
          <DialogContent>
            As mensagens e fontes históricas desta conversa serão excluídas.
          </DialogContent>
          <DialogActions>
            <Button onClick={() => setDeleteOpen(false)}>Cancelar</Button>
            <Button color="error" onClick={() => void removeConversation()}>
              Excluir
            </Button>
          </DialogActions>
        </Dialog>
      </Stack>
    </PageContainer>
  );
}
