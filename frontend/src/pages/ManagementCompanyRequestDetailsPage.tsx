import { useCallback, useEffect, useState } from "react";
import { Alert, Box, Button, Card, CardContent, Chip, Dialog, DialogActions, DialogContent, DialogTitle, Grid, Skeleton, Stack, TextField, Typography } from "@mui/material";
import { useLocation, useNavigate, useParams } from "react-router-dom";
import { PageContainer } from "../components/PageContainer";
import { TransientFeedback } from "../components/TransientFeedback";
import { useAuth } from "../auth/AuthContext";
import { cancelRequest, getRequest, interact } from "../managementCompanyRequests/api";
import { statusLabel, typeLabel } from "../managementCompanyRequests/presentation";
import type { RequestDetail } from "../managementCompanyRequests/types";
import { RequestInformationCards } from "../managementCompanyRequests/RequestInformationCards";
import { ManagementCompanyRequestTimeline } from "../managementCompanyRequests/RequestTimeline";
import { ManagementCompanyRequestConversation } from "../managementCompanyRequests/Conversation";
import { ManagementCompanyRequestActionCard } from "../managementCompanyRequests/ActionCard";

export function ManagementCompanyRequestDetailsPage() {
  const { user } = useAuth(); const { id } = useParams(); const nav = useNavigate(); const location = useLocation();
  const [feedback, setFeedback] = useState((location.state as { feedback?: string } | null)?.feedback ?? "");
  const [data, setData] = useState<RequestDetail>(); const [loading, setLoading] = useState(true); const [error, setError] = useState("");
  const [text, setText] = useState(""); const [files, setFiles] = useState<File[]>([]); const [sending, setSending] = useState(false);
  const [cancelOpen, setCancelOpen] = useState(false); const [reason, setReason] = useState("");
  const load = useCallback(async () => { if (!id) return; setLoading(true); try { setData(await getRequest(id)); setError(""); } catch { setError("Solicitação não encontrada ou fora do seu escopo."); } finally { setLoading(false); } }, [id]);
  useEffect(() => { void load(); }, [load]);
  const reply = async () => { if (!id || !text.trim() || sending) return; setSending(true); try { await interact(id, text.trim(), files); setText(""); setFiles([]); await load(); } catch { setError("Não foi possível enviar. A solicitação pode ter sido atualizada; os dados foram recarregados."); await load(); } finally { setSending(false); } };
  const cancel = async () => { if (!id || !reason.trim() || sending) return; setSending(true); try { await cancelRequest(id, reason.trim()); nav("/management/administrator"); } catch { setError("Não foi possível cancelar a solicitação."); await load(); } finally { setSending(false); } };
  if (loading) return <PageContainer><Skeleton variant="rounded" height={300} /></PageContainer>;
  if (!data) return <PageContainer><Alert severity="error">{error}</Alert><Button onClick={() => nav("/management/administrator")}>Voltar</Button></PageContainer>;
  const terminal = data.status === "Completed" || data.status === "Cancelled";
  return <PageContainer maxWidth={1200}><TransientFeedback message={feedback} severity="success" onClose={() => setFeedback("")} /><Stack spacing={2}>
    {error && <Alert severity="error">{error}</Alert>}<Box display="flex" justifyContent="space-between" gap={2} flexWrap="wrap"><Box><Typography variant="h1">{data.friendlyIdentifier}</Typography><Typography>{typeLabel[data.type]} · {data.condominiumName} · {data.managementCompanyName}</Typography></Box><Chip color={data.status === "WaitingManager" ? "warning" : "default"} label={statusLabel(data.status, data.type)} /></Box>
    {data.status === "WaitingManager" && <Alert severity="warning">A administradora precisa de uma resposta sua para continuar.</Alert>}
    <Grid container spacing={3}><Grid size={{ xs: 12, lg: 8 }}><Stack spacing={2}><RequestInformationCards request={data} />
      {!terminal && <ManagementCompanyRequestActionCard><Button color="error" variant="outlined" onClick={() => setCancelOpen(true)}>Cancelar solicitação</Button></ManagementCompanyRequestActionCard>}
      <Card variant="outlined"><CardContent><Typography variant="h2" mb={2}>Conversa</Typography><ManagementCompanyRequestConversation request={data} currentUserId={user?.id} text={text} files={files} sending={sending} readOnly={terminal} error={error} onText={setText} onFiles={setFiles} onError={setError} onSend={reply} /></CardContent></Card>
    </Stack></Grid><Grid size={{ xs: 12, lg: 4 }}><Card variant="outlined"><CardContent><Typography variant="h2" mb={3}>Timeline</Typography><ManagementCompanyRequestTimeline request={data} /></CardContent></Card></Grid></Grid>
  </Stack><Dialog open={cancelOpen} onClose={() => !sending && setCancelOpen(false)}><DialogTitle>Cancelar solicitação?</DialogTitle><DialogContent><Typography mb={2}>Após o cancelamento, esta solicitação não poderá ser reaberta.</Typography><TextField autoFocus fullWidth multiline minRows={3} label="Motivo do cancelamento" value={reason} inputProps={{ maxLength: 500 }} onChange={event => setReason(event.target.value)} /></DialogContent><DialogActions><Button onClick={() => setCancelOpen(false)}>Voltar</Button><Button color="error" disabled={!reason.trim() || sending} onClick={() => void cancel()}>Cancelar solicitação</Button></DialogActions></Dialog></PageContainer>;
}
