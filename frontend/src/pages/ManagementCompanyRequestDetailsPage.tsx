import { useCallback, useEffect, useState } from "react";
import { useLocation, useNavigate, useParams } from "react-router-dom";
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  Skeleton,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import { PageContainer } from "../components/PageContainer";
import {
  cancelRequest,
  getRequest,
  interact,
} from "../managementCompanyRequests/api";
import {
  date,
  money,
  statusLabel,
  typeLabel,
} from "../managementCompanyRequests/presentation";
import type { RequestDetail } from "../managementCompanyRequests/types";
import { AttachmentsPreview } from "../managementCompanyRequests/AttachmentsPreview";
import { TransientFeedback } from "../components/TransientFeedback";
import { selectAttachmentFiles } from "../requests/attachments";
import { useAuth } from "../auth/AuthContext";
import { LocalAttachmentsPreview } from "../managementCompanyRequests/LocalAttachmentsPreview";
export function ManagementCompanyRequestDetailsPage() {
  const { user } = useAuth();
  const { id } = useParams();
  const nav = useNavigate();
  const location = useLocation();
  const [feedback, setFeedback] = useState(
    (location.state as { feedback?: string } | null)?.feedback ?? "",
  );
  const [data, setData] = useState<RequestDetail>();
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [text, setText] = useState("");
  const [files, setFiles] = useState<File[]>([]);
  const [sending, setSending] = useState(false);
  const [cancelOpen, setCancelOpen] = useState(false);
  const [reason, setReason] = useState("");
  const load = useCallback(async () => {
    if (!id) return;
    setLoading(true);
    try {
      setData(await getRequest(id));
      setError("");
    } catch {
      setError("Solicitação não encontrada ou fora do seu escopo.");
    } finally {
      setLoading(false);
    }
  }, [id]);
  useEffect(() => {
    void load();
  }, [load]);
  async function reply() {
    if (!id || !text.trim()) return;
    setSending(true);
    try {
      await interact(id, text, files);
      setText("");
      setFiles([]);
      await load();
    } catch {
      setError(
        "Não foi possível enviar. A solicitação pode ter sido atualizada; os dados foram recarregados.",
      );
      await load();
    } finally {
      setSending(false);
    }
  }
  async function cancel() {
    if (!id) return;
    setSending(true);
    try {
      await cancelRequest(id, reason);
      setCancelOpen(false);
      nav("/management/administrator");
    } catch {
      setError("Não foi possível cancelar a solicitação.");
      await load();
    } finally {
      setSending(false);
    }
  }
  if (loading)
    return (
      <PageContainer>
        <Skeleton variant="rounded" height={300} />
      </PageContainer>
    );
  if (!data)
    return (
      <PageContainer>
        <Alert severity="error">{error}</Alert>
        <Button onClick={() => nav("/management/administrator")}>Voltar</Button>
      </PageContainer>
    );
  const terminal = data.status === "Completed" || data.status === "Cancelled";
  return (
    <PageContainer maxWidth={1100}>
      <TransientFeedback
        message={feedback}
        severity="success"
        onClose={() => setFeedback("")}
      />
      <Stack spacing={2}>
        {error && <Alert severity="error">{error}</Alert>}
        <Box
          display="flex"
          justifyContent="space-between"
          gap={2}
          flexWrap="wrap"
        >
          <Box>
            <Typography variant="h1">{data.friendlyIdentifier}</Typography>
            <Typography>
              {typeLabel[data.type]} · {data.condominiumName} ·{" "}
              {data.managementCompanyName}
            </Typography>
          </Box>
          <Chip
            color={data.status === "WaitingManager" ? "warning" : "default"}
            label={statusLabel(data.status, data.type)}
          />
        </Box>
        {data.status === "WaitingManager" && (
          <Alert severity="warning">
            A administradora precisa de uma resposta sua para continuar.
          </Alert>
        )}
        <Card variant="outlined">
          <CardContent>
            <Typography variant="h2" mb={2}>
              Dados da solicitação
            </Typography>
            {data.fine && (
              <Stack>
                <Typography>
                  <b>Natureza:</b> {data.fine.nature}
                </Typography>
                <Typography>
                  <b>Descrição:</b> {data.fine.description}
                </Typography>
                <Typography>
                  <b>Data:</b> {date(data.fine.occurrenceDate)}
                </Typography>
                <Typography>
                  <b>Valor:</b>{" "}
                  {data.fine.valueNotDefined
                    ? "Valor ainda não definido"
                    : money(data.fine.value!)}
                </Typography>
              </Stack>
            )}
            {data.payment && (
              <Stack>
                <Typography>
                  <b>Natureza:</b> {data.payment.nature}
                </Typography>
                <Typography>
                  <b>Valor:</b> {money(data.payment.value)}
                </Typography>
                <Typography>
                  <b>Data:</b> {date(data.payment.eventDate)}
                </Typography>
                {data.payment.notes && (
                  <Typography>
                    <b>Observações:</b> {data.payment.notes}
                  </Typography>
                )}
                {data.payment.isReimbursement && (
                  <Alert severity="info">
                    Reembolso para {data.payment.beneficiaryName} ·{" "}
                    {data.payment.pixKeyType} · {data.payment.pixKey}
                  </Alert>
                )}
              </Stack>
            )}
            {data.question && (
              <Typography>
                <b>Tema:</b> {data.question.theme}
              </Typography>
            )}
          </CardContent>
        </Card>
        <Card variant="outlined">
          <CardContent>
            <Typography variant="h2" mb={2}>
              Timeline
            </Typography>
            <Stack spacing={2}>
              {data.history.map((h) => ({
                  id: h.id,
                  at: h.createdAt,
                  kind: "system",
                  author: "Sistema",
                  text:
                    h.reason ||
                    `Status alterado para ${statusLabel(h.newStatus, data.type)}`,
                }))
                .map((item) => (
                  <Box key={item.id}>
                    <Typography variant="caption" color="text.secondary">
                      {item.author} ·{" "}
                      {new Date(item.at).toLocaleString("pt-BR")}
                    </Typography>
                    <Typography>{item.text}</Typography>
                    <Divider sx={{ mt: 2 }} />
                  </Box>
                ))}
            </Stack>
          </CardContent>
        </Card>
        <Card variant="outlined">
          <CardContent>
            <Typography variant="h2" mb={2}>Conversa</Typography>
            <Stack spacing={1.5} sx={{ maxHeight: 440, overflowY: "auto", p: 1.5, border: 1, borderColor: "divider", borderRadius: 2 }}>
              {[...data.messages].sort((a,b) => a.createdAt.localeCompare(b.createdAt)).map(m => {
                const mine = m.authorUserId === user?.id;
                return <Box key={m.id} alignSelf={mine ? "flex-end" : "flex-start"} maxWidth="80%"
                  sx={{ bgcolor: mine ? "primary.main" : "action.hover", color: mine ? "primary.contrastText" : "text.primary", px: 2, py: 1, borderRadius: 2 }}>
                  <Typography variant="caption">{m.authorName} · {m.authorRole} · {new Date(m.createdAt).toLocaleString("pt-BR")}</Typography>
                  <Typography>{m.content}</Typography>
                  <AttachmentsPreview items={data.attachments.filter(a => a.messageId === m.id)} />
                </Box>;
              })}
            </Stack>
          </CardContent>
        </Card>
        <Card variant="outlined">
          <CardContent>
            <Typography variant="h2" mb={2}>
              Anexos
            </Typography>
            <AttachmentsPreview items={data.attachments} />
          </CardContent>
        </Card>
        {!terminal && (
          <Card variant="outlined">
            <CardContent>
              <Stack spacing={1.5}>
                <Typography variant="h2">Conversa com a administradora</Typography>
                <TextField
                  multiline
                  minRows={3}
                  label="Mensagem"
                  value={text}
                  onChange={(e) => setText(e.target.value)}
                />
                <Button component="label" variant="outlined">
                  Adicionar anexos
                  <input
                    hidden
                    multiple
                    type="file"
                    onChange={(e) => {
                      const result = selectAttachmentFiles(
                        files,
                        Array.from(e.target.files ?? []),
                      );
                      setFiles(result.files);
                      if (result.error) setError(result.error);
                    }}
                  />
                </Button>
                <LocalAttachmentsPreview files={files} onRemove={index => setFiles(current => current.filter((_, i) => i !== index))} />
                <Box display="flex" justifyContent="space-between">
                  <Button color="error" onClick={() => setCancelOpen(true)}>
                    Cancelar solicitação
                  </Button>
                  <Button
                    variant="contained"
                    disabled={sending || !text.trim()}
                    onClick={() => void reply()}
                  >
                    {sending ? "Enviando…" : "Enviar"}
                  </Button>
                </Box>
              </Stack>
            </CardContent>
          </Card>
        )}
      </Stack>
      <Dialog open={cancelOpen} onClose={() => setCancelOpen(false)}>
        <DialogTitle>Cancelar solicitação?</DialogTitle>
        <DialogContent>
          <Typography mb={2}>
            Após o cancelamento, esta solicitação não poderá ser reaberta.
          </Typography>
          <TextField
            autoFocus
            fullWidth
            multiline
            minRows={3}
            label="Motivo do cancelamento"
            value={reason}
            inputProps={{ maxLength: 500 }}
            onChange={(e) => setReason(e.target.value)}
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setCancelOpen(false)}>Voltar</Button>
          <Button
            color="error"
            disabled={!reason.trim() || sending}
            onClick={() => void cancel()}
          >
            Cancelar solicitação
          </Button>
        </DialogActions>
      </Dialog>
    </PageContainer>
  );
}
