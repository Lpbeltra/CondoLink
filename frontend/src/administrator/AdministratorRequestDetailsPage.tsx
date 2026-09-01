import { useCallback, useEffect, useState } from "react";
import { Alert, Box, Button, Card, CardContent, Chip, Dialog, DialogActions, DialogContent, DialogTitle, Grid, Skeleton, Stack, TextField, Typography } from "@mui/material";
import { useNavigate, useParams } from "react-router-dom";
import { PageContainer } from "../components/PageContainer";
import { useAuth } from "../auth/AuthContext";
import { cancelRequest, changeRequestStatus, completePayment, getRequest, interact } from "../managementCompanyRequests/api";
import { typeLabel } from "../managementCompanyRequests/presentation";
import type { RequestDetail } from "../managementCompanyRequests/types";
import { RequestInformationCards } from "../managementCompanyRequests/RequestInformationCards";
import { ManagementCompanyRequestTimeline } from "../managementCompanyRequests/RequestTimeline";
import { ManagementCompanyRequestConversation } from "../managementCompanyRequests/Conversation";
import { ManagementCompanyRequestActionCard } from "../managementCompanyRequests/ActionCard";
import { useManagementCompanyRequestRealtime } from "../managementCompanyRequests/realtime";
import { administratorActions, administratorRequestStatusLabel, completionAction } from "./presentation";
import { LocalAttachmentsPreview } from "../managementCompanyRequests/LocalAttachmentsPreview";
import { selectAttachmentFiles } from "../requests/attachments";

export function AdministratorRequestDetailsPage() {
  const { user } = useAuth(); const { id } = useParams(); const nav = useNavigate();
  const [data, setData] = useState<RequestDetail>(); const [loading, setLoading] = useState(true); const [error, setError] = useState("");
  const [text, setText] = useState(""); const [files, setFiles] = useState<File[]>([]); const [sending, setSending] = useState(false);
  const [completeOpen, setCompleteOpen] = useState(false); const [cancelOpen, setCancelOpen] = useState(false); const [reason, setReason] = useState(""); const [processComment, setProcessComment] = useState(""); const [paymentFiles, setPaymentFiles] = useState<File[]>([]);
  const load = useCallback(async (showLoading = false) => { if (!id) return; if (showLoading) setLoading(true); try { setData(await getRequest(id)); setError(""); } catch { setError("Você não possui acesso a esta categoria de solicitação."); } finally { if (showLoading) setLoading(false); } }, [id]);
  useEffect(() => { void load(true); }, [load]);
  useManagementCompanyRequestRealtime({ enabled: Boolean(id), onMessage: () => {}, onUpdated: () => void load(false) });
  const run = async (action: () => Promise<unknown>) => { if (sending) return; setSending(true); try { await action(); setText(""); setFiles([]); await load(false); } catch { setError("Esta solicitação foi atualizada enquanto você estava nesta página. Os dados foram recarregados."); await load(false); } finally { setSending(false); } };
  const reply = async () => { if (!id || !text.trim()) return; await run(() => interact(id, text.trim(), files)); };
  if (loading) return <PageContainer><Skeleton variant="rounded" height={300} /></PageContainer>;
  if (!data) return <PageContainer><Alert severity="error">{error}</Alert><Button onClick={() => nav("/administrator/requests")}>Voltar</Button></PageContainer>;
  const actions = administratorActions(data.status); const completion = completionAction(data.type);
  const submitCompletion = async () => {
    if (!id) return;
    if (data.type === "Payment") await run(() => completePayment(id, paymentFiles));
    else await run(() => changeRequestStatus(id, "Completed", processComment.trim() || null));
    nav("/administrator/requests");
  };
  return <PageContainer maxWidth={1200}><Stack spacing={2}>
    {error && <Alert severity="error">{error}</Alert>}<Box display="flex" justifyContent="space-between" gap={2} flexWrap="wrap"><Box><Typography variant="h1">{data.friendlyIdentifier}</Typography><Typography>{typeLabel[data.type]} · {data.condominiumName}</Typography></Box><Chip label={administratorRequestStatusLabel(data.status, data.type)} /></Box>
    <Grid container spacing={3}><Grid size={{ xs: 12, lg: 8 }}><Stack spacing={2}><RequestInformationCards request={data} showRequester />
      {!actions.readOnly && actions.canInteract && <ManagementCompanyRequestActionCard>{actions.canComplete && <Button variant="contained" color="success" onClick={() => setCompleteOpen(true)}>{completion}</Button>}<Button variant="outlined" color="error" onClick={() => setCancelOpen(true)}>Cancelar</Button></ManagementCompanyRequestActionCard>}
      <Card variant="outlined"><CardContent><Typography variant="h2" mb={2}>Conversa</Typography><ManagementCompanyRequestConversation request={data} currentUserId={user?.id} text={text} files={files} sending={sending} readOnly={actions.readOnly || !actions.canInteract} error={error} onText={setText} onFiles={setFiles} onError={setError} onSend={reply} /></CardContent></Card>
    </Stack></Grid><Grid size={{ xs: 12, lg: 4 }}><Card variant="outlined"><CardContent><Typography variant="h2" mb={3}>Timeline</Typography><ManagementCompanyRequestTimeline request={data} administrator /></CardContent></Card></Grid></Grid>
  </Stack>
  <Dialog open={completeOpen} onClose={() => !sending && setCompleteOpen(false)} fullWidth maxWidth="sm">
    <DialogTitle>{completion}?</DialogTitle>
    <DialogContent>
      {data.type === "Fine" && <TextField autoFocus fullWidth multiline minRows={3} label="Comentário" value={processComment} inputProps={{ maxLength: 1000 }} onChange={event => setProcessComment(event.target.value)} />}
      {data.type === "Payment" && <Stack spacing={2}><Button component="label" variant="outlined">Anexar comprovante<input hidden multiple type="file" onChange={e => { const result = selectAttachmentFiles(paymentFiles, Array.from(e.target.files ?? [])); setPaymentFiles(result.files); setError(result.error ?? ""); }} /></Button><LocalAttachmentsPreview files={paymentFiles} onRemove={index => setPaymentFiles(current => current.filter((_, i) => i !== index))} /></Stack>}
      {data.type === "GeneralQuestion" && <Typography>Após a conclusão, esta solicitação ficará somente para consulta.</Typography>}
    </DialogContent>
    <DialogActions><Button onClick={() => setCompleteOpen(false)}>Voltar</Button><Button color="success" disabled={sending || (data.type === "Payment" && paymentFiles.length === 0 && false)} onClick={() => void submitCompletion()}>{completion}</Button></DialogActions>
  </Dialog>
  <Dialog open={cancelOpen} onClose={() => !sending && setCancelOpen(false)}><DialogTitle>Cancelar solicitação?</DialogTitle><DialogContent><TextField autoFocus fullWidth multiline minRows={3} label="Motivo do cancelamento" value={reason} inputProps={{ maxLength: 500 }} onChange={event => setReason(event.target.value)} /></DialogContent><DialogActions><Button onClick={() => setCancelOpen(false)}>Voltar</Button><Button color="error" disabled={!reason.trim() || sending} onClick={() => void run(async () => { await cancelRequest(id!, reason.trim()); nav("/administrator/requests"); })}>Cancelar solicitação</Button></DialogActions></Dialog>
  </PageContainer>;
}
