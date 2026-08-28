import { useCallback, useEffect, useState } from "react";
import {
  Alert,
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
  changeRequestStatus,
  getRequest,
  interact,
  requestManagerInformation,
} from "../managementCompanyRequests/api";
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
  const { id } = useParams(),
    nav = useNavigate();
  const [data, setData] = useState<RequestDetail>(),
    [loading, setLoading] = useState(true),
    [error, setError] = useState(""),
    [text, setText] = useState(""),
    [files, setFiles] = useState<File[]>([]),
    [sending, setSending] = useState(false),
    [ask, setAsk] = useState(false),
    [complete, setComplete] = useState(false);
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
      setAsk(false);
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
        <Card variant="outlined">
          <CardContent>
            <Typography variant="h2">Condomínio</Typography>
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
        <Card variant="outlined">
          <CardContent>
            <Typography variant="h2">Timeline</Typography>
            {[
              ...data.history.map((h) => ({
                id: h.id,
                at: h.createdAt,
                author: "Sistema",
                text:
                  h.reason ||
                  administratorRequestStatusLabel(h.newStatus, data.type),
              })),
              ...data.messages.map((m) => ({
                id: m.id,
                at: m.createdAt,
                author: `${m.authorName} · ${m.authorRole}`,
                text: m.content,
              })),
            ]
              .sort((a, b) => a.at.localeCompare(b.at))
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
            <Typography variant="h2">Anexos</Typography>
            <AttachmentsPreview items={data.attachments} />
          </CardContent>
        </Card>
        {!terminal && (
          <Card variant="outlined">
            <CardContent>
              <Stack spacing={1}>
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
                    <Button
                      disabled={sending || !text.trim()}
                      onClick={() =>
                        void run(
                          () => interact(id!, text, files),
                          "Mensagem enviada.",
                        )
                      }
                    >
                      Enviar mensagem
                    </Button>
                    <Button onClick={() => setAsk(true)}>
                      Solicitar informação
                    </Button>
                    {actions.canComplete && (
                      <Button
                        variant="contained"
                        onClick={() => setComplete(true)}
                      >
                        {completion}
                      </Button>
                    )}
                  </>
                )}
                {actions.canStart && (
                  <Button
                    variant="contained"
                    disabled={sending}
                    onClick={() =>
                      void run(
                        () => changeRequestStatus(id!, "InProgress"),
                        "Processamento iniciado.",
                      )
                    }
                  >
                    Iniciar processamento
                  </Button>
                )}
              </Stack>
            </CardContent>
          </Card>
        )}
        {data.status === "WaitingManager" && (
          <Alert severity="info">Aguardando resposta da gestão.</Alert>
        )}
        <Dialog open={ask} onClose={() => setAsk(false)} fullWidth>
          <DialogTitle>Solicitar informação à gestão</DialogTitle>
          <DialogContent>
            <TextField
              autoFocus
              fullWidth
              multiline
              minRows={4}
              label="Explique o que é necessário"
              value={text}
              onChange={(e) => setText(e.target.value)}
            />
          </DialogContent>
          <DialogActions>
            <Button onClick={() => setAsk(false)}>Cancelar</Button>
            <Button
              disabled={!text.trim() || sending}
              onClick={() =>
                void run(
                  () => requestManagerInformation(id!, text, files),
                  "Informação solicitada.",
                )
              }
            >
              Solicitar informação
            </Button>
          </DialogActions>
        </Dialog>
        <Dialog open={complete} onClose={() => setComplete(false)}>
          <DialogTitle>{completion}?</DialogTitle>
          <DialogContent>
            Após a conclusão, esta solicitação ficará somente para consulta.
          </DialogContent>
          <DialogActions>
            <Button onClick={() => setComplete(false)}>Voltar</Button>
            <Button
              disabled={sending}
              onClick={() =>
                void run(
                  () => changeRequestStatus(id!, "Completed"),
                  "Solicitação concluída.",
                )
              }
            >
              {completion}
            </Button>
          </DialogActions>
        </Dialog>
      </Stack>
    </PageContainer>
  );
}
