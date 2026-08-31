import { useCallback, useEffect, useState } from "react";
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
  Skeleton,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import { useNavigate, useParams } from "react-router-dom";
import { PageContainer } from "../components/PageContainer";
import { AttachmentsPreview } from "../managementCompanyRequests/AttachmentsPreview";
import {
  cancelRequest,
  changeRequestStatus,
  getRequest,
  interact,
} from "../managementCompanyRequests/api";
import { useAuth } from "../auth/AuthContext";
import { LocalAttachmentsPreview } from "../managementCompanyRequests/LocalAttachmentsPreview";
import {
  date,
  money,
  typeLabel,
} from "../managementCompanyRequests/presentation";
import type { RequestDetail } from "../managementCompanyRequests/types";
import { selectAttachmentFiles } from "../requests/attachments";
import {
  administratorActions,
  administratorRequestStatusLabel,
  completionAction,
} from "./presentation";

export function AdministratorRequestDetailsPage() {
  const { user } = useAuth();
  const { id } = useParams(),
    nav = useNavigate();
  const [data, setData] = useState<RequestDetail>(),
    [loading, setLoading] = useState(true),
    [error, setError] = useState(""),
    [text, setText] = useState(""),
    [files, setFiles] = useState<File[]>([]),
    [sending, setSending] = useState(false),
    [complete, setComplete] = useState(false),
    [cancelOpen, setCancelOpen] = useState(false),
    [reason, setReason] = useState("");
  const load = useCallback(async () => {
    if (!id) return;
    setLoading(true);
    try {
      setData(await getRequest(id));
      setError("");
    } catch {
      setError("Você não possui acesso a esta categoria de solicitação.");
    } finally {
      setLoading(false);
    }
  }, [id]);
  useEffect(() => {
    void load();
  }, [load]);
  async function run(action: () => Promise<unknown>, success: string) {
    setSending(true);
    try {
      await action();
      setText("");
      setFiles([]);
      setComplete(false);
      await load();
      setError(success);
    } catch {
      setError(
        "Esta solicitação foi atualizada enquanto você estava nesta página. Os dados foram recarregados.",
      );
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
        <Button onClick={() => nav("/administrator/requests")}>Voltar</Button>
      </PageContainer>
    );
  const actions = administratorActions(data.status),
    terminal = actions.readOnly,
    canOperate = actions.canInteract,
    completion = completionAction(data.type);
  return (
    <PageContainer maxWidth={1100}>
      <Stack spacing={2}>
        {error && (
          <Alert
            severity={
              error.includes("enviada") ||
              error.includes("iniciado") ||
              error.includes("concluída") ||
              error.includes("solicitada")
                ? "success"
                : "warning"
            }
          >
            {error}
          </Alert>
        )}
        <Stack direction="row" justifyContent="space-between">
          <div>
            <Typography variant="h1">{data.friendlyIdentifier}</Typography>
            <Typography>
              {typeLabel[data.type]} · {data.condominiumName}
            </Typography>
          </div>
          <Chip
            label={administratorRequestStatusLabel(data.status, data.type)}
          />
        </Stack>
        <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", md: "minmax(0, 1fr) minmax(0, 1.35fr)" }, gap: 2, alignItems: "stretch" }}>
        <Card variant="outlined">
          <CardContent>
            <Typography variant="h2">Informações do solicitante</Typography>
            <Typography>
              {data.condominium?.name ?? data.condominiumName}
            </Typography>
            {data.condominium?.address && (
              <Typography>
                {data.condominium.address}
                {data.condominium.city
                  ? ` · ${data.condominium.city}/${data.condominium.state}`
                  : ""}
              </Typography>
            )}
            {data.condominium?.managers.map((x) => (
              <Typography key={x.id}>
                {x.role === "Manager" ? "Síndico" : "Subsíndico"}: {x.fullName}
              </Typography>
            ))}
          </CardContent>
        </Card>
        <Card variant="outlined">
          <CardContent>
            <Typography variant="h2">Dados da solicitação</Typography>
            {data.fine && (
              <Stack>
                <Typography>
                  Unidade: {data.fine.block ? `${data.fine.block} / ` : ""}
                  {data.fine.unit ?? data.fine.unitId}
                </Typography>
                <Typography>Natureza: {data.fine.nature}</Typography>
                <Typography>Descrição: {data.fine.description}</Typography>
                <Typography>Data: {date(data.fine.occurrenceDate)}</Typography>
                <Typography>
                  Valor:{" "}
                  {data.fine.valueNotDefined
                    ? "Valor ainda não definido"
                    : money(data.fine.value!)}
                </Typography>
              </Stack>
            )}
            {data.payment && (
              <Stack>
                <Typography>Natureza: {data.payment.nature}</Typography>
                <Typography>Valor: {money(data.payment.value)}</Typography>
                <Typography>Data: {date(data.payment.eventDate)}</Typography>
                {data.payment.isReimbursement && (
                  <Alert severity="info">
                    Reembolso: {data.payment.beneficiaryName} ·{" "}
                    {data.payment.pixKeyType} · {data.payment.pixKey}
                  </Alert>
                )}
              </Stack>
            )}
            {data.question && (
              <Typography>Tema: {data.question.theme}</Typography>
            )}
          </CardContent>
        </Card>
        </Box>
        <Card variant="outlined">
          <CardContent>
            <Typography variant="h2">Timeline</Typography>
            {data.history.map((h) => ({
                id: h.id,
                at: h.createdAt,
                author: "Sistema",
                text:
                  h.reason ||
                  administratorRequestStatusLabel(h.newStatus, data.type),
              }))
              .map((x) => (
                <Stack key={x.id} py={1}>
                  <Typography variant="caption">
                    {x.author} · {new Date(x.at).toLocaleString("pt-BR")}
                  </Typography>
                  <Typography>{x.text}</Typography>
                </Stack>
              ))}
          </CardContent>
        </Card>
        <Card variant="outlined">
          <CardContent>
            <Typography variant="h2">Conversa</Typography>
            <Stack spacing={1.5} mt={2} sx={{ maxHeight: 440, overflowY: "auto", p: 1.5, border: 1, borderColor: "divider", borderRadius: 2 }}>
              {[...data.messages].sort((a,b) => a.createdAt.localeCompare(b.createdAt)).map((m) => {
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
            <Typography variant="h2">Anexos</Typography>
            <AttachmentsPreview items={data.attachments} />
          </CardContent>
        </Card>
        {!terminal && (
          <Card variant="outlined">
            <CardContent>
              <Stack spacing={1.25}>
                <Typography variant="h2">Atendimento</Typography>
                {canOperate && (
                  <>
                    <TextField
                      label="Mensagem"
                      multiline
                      minRows={3}
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
                          const r = selectAttachmentFiles(
                            files,
                            Array.from(e.target.files ?? []),
                          );
                          setFiles(r.files);
                          if (r.error) setError(r.error);
                        }}
                      />
                    </Button>
                    <LocalAttachmentsPreview files={files} onRemove={index => setFiles(current => current.filter((_, i) => i !== index))} />
                    <Button
                      disabled={sending || !text.trim()}
                      onClick={() =>
                        void run(
                          () => interact(id!, text, files),
                          "Mensagem enviada.",
                        )
                      }
                    >
                      Enviar
                    </Button>
                    <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap>
                      {data.status === "InProgress" && <Button type="button" size="small" variant="outlined" onClick={() => void run(
                        () => changeRequestStatus(id!, "WaitingManager"),
                        "Solicitação marcada como aguardando.")}>Marcar como aguardando</Button>}
                      {actions.canComplete && <Button type="button" size="small" variant="contained" color="success" onClick={() => setComplete(true)}>{completion}</Button>}
                      <Button type="button" size="small" variant="outlined" color="error" onClick={() => setCancelOpen(true)}>Cancelar solicitação</Button>
                    </Stack>
                  </>
                )}
              </Stack>
            </CardContent>
          </Card>
        )}
        {data.status === "WaitingManager" && (
          <Alert severity="info">Aguardando resposta da gestão.</Alert>
        )}
        <Dialog open={complete} onClose={() => setComplete(false)}>
          <DialogTitle>{completion}?</DialogTitle>
          <DialogContent>
            Após a conclusão, esta solicitação ficará somente para consulta.
          </DialogContent>
          <DialogActions>
            <Button onClick={() => setComplete(false)}>Voltar</Button>
            <Button
              disabled={sending}
              onClick={() => void (async () => { await changeRequestStatus(id!, "Completed"); nav("/administrator/requests"); })()}
            >
              {completion}
            </Button>
          </DialogActions>
        </Dialog>
        <Dialog open={cancelOpen} onClose={() => setCancelOpen(false)}>
          <DialogTitle>Cancelar solicitação?</DialogTitle>
          <DialogContent><TextField autoFocus fullWidth multiline minRows={3} label="Motivo do cancelamento"
            value={reason} inputProps={{ maxLength: 500 }} onChange={e => setReason(e.target.value)} /></DialogContent>
          <DialogActions><Button onClick={() => setCancelOpen(false)}>Voltar</Button>
            <Button color="error" disabled={!reason.trim() || sending} onClick={() => void (async () => {
              setSending(true); try { await cancelRequest(id!, reason); nav("/administrator/requests"); }
              catch { setError("Não foi possível cancelar a solicitação."); await load(); } finally { setSending(false); }
            })()}>Cancelar solicitação</Button></DialogActions>
        </Dialog>
      </Stack>
    </PageContainer>
  );
}
