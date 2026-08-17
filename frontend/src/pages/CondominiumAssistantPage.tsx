import { useEffect, useRef, useState } from "react";
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
  MenuItem,
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
import {
  askAssistant,
  deleteConversation,
  deleteDocument,
  DOCUMENT_FILE_TOO_LARGE_MESSAGE,
  getConversation,
  getDocumentUploadError,
  listConversations,
  listDocuments,
  MAXIMUM_DOCUMENT_FILE_BYTES,
  MAXIMUM_DOCUMENT_FILE_MEGABYTES,
  removeRequestContext,
  setDocumentActive,
  startConversation,
  uploadDocument,
  type AssistantConversation,
  type AssistantDocument,
  type AssistantMessage,
  type AssistantSource,
} from "../assistant/api";

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
  const endRef = useRef<HTMLDivElement>(null);
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
    } catch {
      setHistoryError("Não foi possível carregar suas conversas.");
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
    } catch {
      setError("Não foi possível abrir esta conversa.");
    } finally {
      setOpening(false);
    }
  };
  const fresh = () => {
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
    setQuestion("");
    setSending(true);
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
    try {
      if (!conversation) {
        const result = await startConversation(
          activeCondominiumId,
          value,
          pendingRequestId,
        );
        setConversation(result.conversation);
        setPendingRequestId(undefined);
        setMessages((x) => [
          ...x,
          {
            id: `answer-${Date.now()}`,
            role: "Assistant",
            content: result.answer,
            createdAt: new Date().toISOString(),
            sources: result.sources.map((source) => ({
              source,
              documentCurrentlyActive: true,
            })),
          },
        ]);
      } else {
        const result = await askAssistant(
          activeCondominiumId,
          conversation.id,
          value,
        );
        setMessages((x) => [
          ...x,
          {
            id: `answer-${Date.now()}`,
            role: "Assistant",
            content: result.answer,
            createdAt: new Date().toISOString(),
            sources: result.sources.map((source) => ({
              source,
              documentCurrentlyActive: true,
            })),
          },
        ]);
      }
      await loadHistory();
    } catch {
      setError(
        "O assistente está temporariamente indisponível. Sua pergunta foi preservada quando a conversa já havia sido criada.",
      );
    } finally {
      setSending(false);
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
                      Consulte documentos, regras e informações do condomínio.
                    </Typography>
                    {[
                      "Quais são as regras para mudanças?",
                      "O que o regimento diz sobre barulho?",
                      "Quais são as regras da piscina?",
                    ].map((value) => (
                      <Button
                        key={value}
                        variant="outlined"
                        onClick={() => setQuestion(value)}
                        sx={{ alignSelf: "flex-start" }}
                      >
                        {value}
                      </Button>
                    ))}
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
                            ({ source, documentExists = true, documentCurrentlyActive }) => documentExists ? (
                              <Chip
                                key={`${message.id}-${source.marker}`}
                                component="a"
                                clickable
                                href={`/api/condominiums/${activeCondominiumId}/documents/${source.documentId}/download`}
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
                {sending && (
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

export function CondominiumDocumentsPage() {
  const { activeCondominiumId } = useManagementContext();
  const [documents, setDocuments] = useState<AssistantDocument[]>([]);
  const [file, setFile] = useState<File | null>(null);
  const [name, setName] = useState("");
  const [type, setType] = useState("Other");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  const [deleting, setDeleting] = useState<AssistantDocument | null>(null);
  const load = async () => {
    if (activeCondominiumId)
      setDocuments(await listDocuments(activeCondominiumId));
  };
  useEffect(() => {
    void load();
  }, [activeCondominiumId]);
  const selectFile = (selected: File | null) => {
    if (selected && selected.size > MAXIMUM_DOCUMENT_FILE_BYTES) {
      setFile(null);
      setError(DOCUMENT_FILE_TOO_LARGE_MESSAGE);
      return;
    }
    setFile(selected);
    setError("");
  };
  const upload = async () => {
    if (!activeCondominiumId || !file || !name.trim()) return;
    if (file.size > MAXIMUM_DOCUMENT_FILE_BYTES) {
      setError(DOCUMENT_FILE_TOO_LARGE_MESSAGE);
      return;
    }
    const form = new FormData();
    form.append("file", file);
    form.append("name", name);
    form.append("documentType", type);
    setLoading(true);
    setError("");
    try {
      await uploadDocument(activeCondominiumId, form);
      setFile(null);
      setName("");
      await load();
    } catch (uploadError) {
      setError(getDocumentUploadError(uploadError));
    } finally {
      setLoading(false);
    }
  };
  const confirmDelete = async () => {
    if (!activeCondominiumId || !deleting) return;
    setError("");
    try {
      await deleteDocument(activeCondominiumId, deleting.id);
      setDocuments((current) =>
        current.filter((document) => document.id !== deleting.id),
      );
      setDeleting(null);
    } catch {
      setError("Não foi possível excluir o documento. Tente novamente.");
    }
  };
  return (
    <PageContainer>
      <Stack gap={2}>
        <Typography variant="h1">Documentos</Typography>
        {error && <Alert severity="error">{error}</Alert>}
        <Card>
          <CardContent>
            <Stack direction={{ xs: "column", md: "row" }} gap={2}>
              <TextField
                label="Nome"
                value={name}
                onChange={(e) => setName(e.target.value)}
              />
              <TextField
                select
                label="Tipo"
                value={type}
                onChange={(e) => setType(e.target.value)}
                sx={{ minWidth: 200 }}
              >
                {[
                  "Convention",
                  "InternalRules",
                  "Minutes",
                  "Contract",
                  "Manual",
                  "Notice",
                  "Other",
                ].map((value) => (
                  <MenuItem key={value} value={value}>
                    {value}
                  </MenuItem>
                ))}
              </TextField>
              <Button component="label" variant="outlined">
                {file?.name ?? "Selecionar PDF, DOCX ou TXT"}
                <input
                  hidden
                  type="file"
                  accept=".pdf,.docx,.txt"
                  onChange={(e) => selectFile(e.target.files?.[0] ?? null)}
                />
              </Button>
              <Button
                variant="contained"
                disabled={loading || !file || !name.trim()}
                onClick={() => void upload()}
              >
                Enviar
              </Button>
            </Stack>
            <Typography variant="caption" color="text.secondary">
              PDF, DOCX ou TXT · máximo {MAXIMUM_DOCUMENT_FILE_MEGABYTES} MB
            </Typography>
          </CardContent>
        </Card>
        {documents.map((document) => (
          <Card key={document.id}>
            <CardContent>
              <Stack
                direction={{ xs: "column", sm: "row" }}
                justifyContent="space-between"
              >
                <div>
                  <Typography fontWeight={800}>{document.name}</Typography>
                  <Typography color="text.secondary">
                    {document.originalFileName} · versão {document.version}
                  </Typography>
                  {document.processingError && (
                    <Alert severity="warning" sx={{ mt: 1 }}>
                      {document.processingError}
                    </Alert>
                  )}
                </div>
                <Stack direction="row" gap={1} alignItems="center">
                  <Chip
                    label={document.processingStatus}
                    color={
                      document.processingStatus === "Ready"
                        ? "success"
                        : "default"
                    }
                  />
                  <Button
                    onClick={async () => {
                      await setDocumentActive(
                        activeCondominiumId!,
                        document.id,
                        !document.isActive,
                      );
                      await load();
                    }}
                  >
                    {document.isActive ? "Inativar" : "Ativar"}
                  </Button>
                  <Button
                    component="a"
                    href={`/api/condominiums/${activeCondominiumId}/documents/${document.id}/download`}
                  >
                    Baixar
                  </Button>
                  <IconButton
                    aria-label={`Excluir ${document.name}`}
                    title="Excluir"
                    onClick={() => setDeleting(document)}
                  >
                    <DeleteOutlineRoundedIcon />
                  </IconButton>
                </Stack>
              </Stack>
            </CardContent>
          </Card>
        ))}
        <Dialog open={Boolean(deleting)} onClose={() => setDeleting(null)}>
          <DialogTitle>Excluir documento?</DialogTitle>
          <DialogContent>
            “{deleting?.name}” será removido definitivamente. Esta ação não pode
            ser desfeita.
          </DialogContent>
          <DialogActions>
            <Button onClick={() => setDeleting(null)}>Cancelar</Button>
            <Button color="error" onClick={() => void confirmDelete()}>
              Excluir
            </Button>
          </DialogActions>
        </Dialog>
      </Stack>
    </PageContainer>
  );
}
