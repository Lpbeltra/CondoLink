import { useCallback, useEffect, useState } from "react";
import {
  Alert,
  Button,
  Card,
  CardActionArea,
  CardContent,
  Chip,
  Checkbox,
  FormControlLabel,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  MenuItem,
  Pagination,
  Skeleton,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import { useNavigate } from "react-router-dom";
import { PageContainer } from "../components/PageContainer";
import {
  getAdministratorOptions,
  listAdministratorRequests,
  startRequestProcessing,
} from "../managementCompanyRequests/api";
import { date, money, typeLabel } from "../managementCompanyRequests/presentation";
import type {
  AdministratorOptions,
  ManagementCompanyRequestStatus,
  PageResult,
} from "../managementCompanyRequests/types";
import { useAdministrator } from "./AdministratorContext";
import {
  administratorRequestStatusLabel,
  administratorStatusLabel as labels,
} from "./presentation";

export function AdministratorRequestsPage() {
  const nav = useNavigate(),
    { value: context } = useAdministrator();
  const [options, setOptions] = useState<AdministratorOptions>({
    condominiums: [],
    categories: [],
  });
  const [data, setData] = useState<PageResult>(),
    [loading, setLoading] = useState(true),
    [error, setError] = useState("");
  const [pendingStart, setPendingStart] = useState<string>();
  const [page, setPage] = useState(1),
    [search, setSearch] = useState(""),
    [condominiumId, setCondominiumId] = useState(""),
    [categoryId, setCategoryId] = useState(""),
    [status, setStatus] = useState(""),
    [from, setFrom] = useState(""),
    [to, setTo] = useState("");
  const [includeCompleted, setIncludeCompleted] = useState(false),
    [includeCancelled, setIncludeCancelled] = useState(false);
  useEffect(() => {
    getAdministratorOptions()
      .then(setOptions)
      .catch(() => setError("Não foi possível carregar os filtros."));
  }, []);
  const load = useCallback(async () => {
    if (from && to && from > to) {
      setError("A data inicial não pode ser posterior à data final.");
      return;
    }
    setLoading(true);
    try {
      setData(
        await listAdministratorRequests({
          page,
          search: search || undefined,
          condominiumId: condominiumId || undefined,
          categoryId: categoryId || undefined,
          status: (status || undefined) as
            | ManagementCompanyRequestStatus
            | undefined,
          from: from || undefined,
          to: to || undefined,
          includeCompleted,
          includeCancelled,
        }),
      );
      setError("");
    } catch {
      setError("Não foi possível carregar as solicitações.");
    } finally {
      setLoading(false);
    }
  }, [page, search, condominiumId, categoryId, status, from, to, includeCompleted, includeCancelled]);
  useEffect(() => {
    const timer = setTimeout(() => void load(), 300);
    return () => clearTimeout(timer);
  }, [load]);
  useEffect(() => {
    const refresh = () => void load();
    const interval = window.setInterval(refresh, 30_000);
    window.addEventListener("focus", refresh);
    return () => { window.clearInterval(interval); window.removeEventListener("focus", refresh); };
  }, [load]);
  const filter =
    (setter: (v: string) => void) =>
    (e: React.ChangeEvent<HTMLInputElement>) => {
      setter(e.target.value);
      setPage(1);
    };
  if (context && context.categories.length === 0)
    return (
      <PageContainer>
        <Alert severity="info">
          Seu acesso ainda não possui categorias de atendimento atribuídas.
        </Alert>
      </PageContainer>
    );
  return (
    <PageContainer maxWidth={1100}>
      <Stack spacing={2}>
        <div>
          <Typography variant="h1">Solicitações</Typography>
        </div>
        {error && <Alert severity="error">{error}</Alert>}
        <Stack direction={{ xs: "column", md: "row" }} spacing={1}>
          <TextField
            label="Buscar"
            value={search}
            onChange={filter(setSearch)}
            fullWidth
          />
          <TextField
            select
            label="Condomínio"
            value={condominiumId}
            onChange={filter(setCondominiumId)}
            sx={{ minWidth: 190 }}
          >
            <MenuItem value="">Todos</MenuItem>
            {options.condominiums.map((x) => (
              <MenuItem key={x.condominiumId} value={x.condominiumId}>
                {x.name}
              </MenuItem>
            ))}
          </TextField>
          <TextField
            select
            label="Categoria"
            value={categoryId}
            onChange={filter(setCategoryId)}
            sx={{ minWidth: 190 }}
          >
            <MenuItem value="">Todas</MenuItem>
            {options.categories.map((x) => (
              <MenuItem key={x.id} value={x.id}>
                {x.name}
              </MenuItem>
            ))}
          </TextField>
          <TextField
            select
            label="Status"
            value={status}
            onChange={filter(setStatus)}
            sx={{ minWidth: 170 }}
          >
            <MenuItem value="">Todos</MenuItem>
            {Object.entries(labels).map(([v, l]) => (
              <MenuItem key={v} value={v}>
                {l}
              </MenuItem>
            ))}
          </TextField>
        </Stack>
        <Stack direction={{ xs: "column", sm: "row" }} spacing={1}>
          <TextField
            label="Data inicial"
            type="date"
            value={from}
            onChange={filter(setFrom)}
            slotProps={{ inputLabel: { shrink: true } }}
          />
          <TextField
            label="Data final"
            type="date"
            value={to}
            onChange={filter(setTo)}
            slotProps={{ inputLabel: { shrink: true } }}
          />
          <Button
            onClick={() => {
              setSearch("");
              setCondominiumId("");
              setCategoryId("");
              setStatus("");
              setFrom("");
              setTo("");
              setPage(1);
            }}
          >
            Limpar filtros
          </Button>
        </Stack>
        <Stack direction={{ xs: "column", sm: "row" }} spacing={1}>
          <FormControlLabel control={<Checkbox checked={includeCompleted} onChange={e => { setIncludeCompleted(e.target.checked); setPage(1); }} />} label="Exibir solicitações processadas" />
          <FormControlLabel control={<Checkbox checked={includeCancelled} onChange={e => { setIncludeCancelled(e.target.checked); setPage(1); }} />} label="Exibir solicitações canceladas" />
        </Stack>
        {loading ? (
          <Skeleton variant="rounded" height={240} />
        ) : !data?.items.length ? (
          <Alert severity="info">
            {search || condominiumId || categoryId || status || from || to
              ? "Nenhuma solicitação encontrada com os filtros selecionados."
              : "Nenhuma solicitação disponível no momento."}
          </Alert>
        ) : (
          data.items.map((item) => (
            <Card key={item.id} variant="outlined">
              <CardActionArea
                onClick={() => item.status === "Submitted"
                  ? setPendingStart(item.id)
                  : nav(`/administrator/requests/${item.id}`)}
              >
                <CardContent>
                  <Stack direction="row" justifyContent="space-between" gap={1}>
                    <div>
                      <Typography fontWeight={800}>
                        {item.friendlyIdentifier}
                      </Typography>
                      <Stack direction="row" spacing={1} alignItems="center" flexWrap="wrap" useFlexGap>
                        <Chip size="small" variant="outlined" label={typeLabel[item.type]} />
                        <Typography>{item.condominiumName}</Typography>
                      </Stack>
                      <Typography color="text.secondary">
                        {item.subject}
                      </Typography>
                      <Typography variant="caption">
                        Aberta em {date(item.createdAt)}
                      </Typography>
                      {item.status !== "Submitted" && <Typography variant="body2" color="text.secondary">
                        {item.type === "Fine" && item.unit ? `${item.block ? `${item.block} / ` : ""}${item.unit}${item.value != null ? ` · ${money(item.value)}` : ""}` : null}
                        {item.type === "Payment" ? `${item.value != null ? money(item.value) : ""}${item.beneficiaryName ? ` · ${item.beneficiaryName}` : ""}` : null}
                      </Typography>}
                    </div>
                    <Chip
                      color={item.status === "Submitted" ? "primary" : item.status === "WaitingManager" ? "warning" : item.status === "Completed" ? "success" : item.status === "Cancelled" ? "error" : "default"}
                      label={administratorRequestStatusLabel(
                        item.status,
                        item.type,
                      )}
                    />
                  </Stack>
                </CardContent>
              </CardActionArea>
            </Card>
          ))
        )}
        {data && data.total > data.pageSize && (
          <Pagination
            page={page}
            count={Math.ceil(data.total / data.pageSize)}
            onChange={(_, value) => setPage(value)}
          />
        )}
        <Dialog open={Boolean(pendingStart)} onClose={() => setPendingStart(undefined)}>
          <DialogTitle>Iniciar processamento</DialogTitle>
          <DialogContent>Deseja iniciar o processamento desta solicitação?</DialogContent>
          <DialogActions>
            <Button onClick={() => setPendingStart(undefined)}>Cancelar</Button>
            <Button variant="contained" onClick={async () => {
              const requestId = pendingStart!;
              try { await startRequestProcessing(requestId); nav(`/administrator/requests/${requestId}`); }
              catch { setPendingStart(undefined); await load(); setError("A solicitação foi atualizada. Verifique o status e tente novamente."); }
            }}>Confirmar</Button>
          </DialogActions>
        </Dialog>
      </Stack>
    </PageContainer>
  );
}
