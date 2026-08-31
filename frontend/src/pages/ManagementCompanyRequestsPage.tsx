import { useCallback, useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import {
  Alert,
  Box,
  Button,
  Card,
  CardActionArea,
  CardContent,
  Chip,
  MenuItem,
  Pagination,
  Skeleton,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import AddRoundedIcon from "@mui/icons-material/AddRounded";
import { PageContainer } from "../components/PageContainer";
import { EmptyState } from "../components/EmptyState";
import { useManagementContext } from "../management/ManagementContext";
import { listRequests } from "../managementCompanyRequests/api";
import {
  statusLabel,
  typeLabel,
} from "../managementCompanyRequests/presentation";
import type {
  ManagementCompanyRequestStatus as Status,
  ManagementCompanyRequestType as Type,
  PageResult,
} from "../managementCompanyRequests/types";
export function ManagementCompanyRequestsPage() {
  const nav = useNavigate(),
    { activeCondominiumId, activeCondominium, condominiums } =
      useManagementContext();
  const [data, setData] = useState<PageResult>(),
    [loading, setLoading] = useState(true),
    [error, setError] = useState(""),
    [search, setSearch] = useState(""),
    [type, setType] = useState<Type | "">(""),
    [status, setStatus] = useState<Status | "">(""),
    [condo, setCondo] = useState(""),
    [from, setFrom] = useState(""),
    [to, setTo] = useState(""),
    [page, setPage] = useState(1);
  const invalid = Boolean(from && to && from > to);
  const load = useCallback(async () => {
    if (invalid) return;
    setLoading(true);
    try {
      setData(
        await listRequests({
          condominiumId: (activeCondominiumId ?? condo) || undefined,
          type: type || undefined,
          status: status || undefined,
          search: search || undefined,
          from: from || undefined,
          to: to || undefined,
          page,
        }),
      );
      setError("");
    } catch {
      setError("Não foi possível carregar as solicitações da administradora.");
    } finally {
      setLoading(false);
    }
  }, [
    activeCondominiumId,
    condo,
    from,
    invalid,
    page,
    search,
    status,
    to,
    type,
  ]);
  useEffect(() => {
    const timer = setTimeout(() => void load(), 300);
    return () => clearTimeout(timer);
  }, [load]);
  useEffect(() => {
    const refresh = () => void load();
    window.addEventListener("focus", refresh);
    return () => window.removeEventListener("focus", refresh);
  }, [load]);
  useEffect(
    () => setPage(1),
    [activeCondominiumId, condo, from, search, status, to, type],
  );
  const filtered = Boolean(search || type || status || condo || from || to);
  const clear = () => {
    setSearch("");
    setType("");
    setStatus("");
    setCondo("");
    setFrom("");
    setTo("");
  };
  return (
    <PageContainer maxWidth={1200}>
      <Stack spacing={2}>
        <Box
          display="flex"
          justifyContent="space-between"
          alignItems={{ xs: "stretch", sm: "center" }}
          flexDirection={{ xs: "column", sm: "row" }}
          gap={2}
        >
          <Box>
            <Typography variant="h1">Administradora</Typography>
            <Typography color="text.secondary">
              Acompanhe solicitações enviadas{" "}
              {activeCondominium
                ? `à administradora de ${activeCondominium.name}`
                : "às administradoras dos seus condomínios"}
              .
            </Typography>
          </Box>
          <Button
            variant="contained"
            startIcon={<AddRoundedIcon />}
            onClick={() => nav("/management/administrator/new")}
          >
            Nova solicitação
          </Button>
        </Box>
        {error && <Alert severity="error">{error}</Alert>}
        {invalid && (
          <Alert severity="warning">
            A data inicial não pode ser posterior à data final.
          </Alert>
        )}
        <Stack
          direction={{ xs: "column", md: "row" }}
          spacing={1.5}
          flexWrap="wrap"
        >
          {!activeCondominiumId && (
            <TextField
              select
              label="Condomínio"
              size="small"
              value={condo}
              onChange={(e) => setCondo(e.target.value)}
              sx={{ minWidth: 210 }}
            >
              <MenuItem value="">Todos</MenuItem>
              {condominiums.map((c) => (
                <MenuItem value={c.id} key={c.id}>
                  {c.name}
                </MenuItem>
              ))}
            </TextField>
          )}
          <TextField
            label="Buscar"
            size="small"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            sx={{ flex: 1, minWidth: 190 }}
          />
          <TextField
            select
            label="Tipo"
            size="small"
            value={type}
            onChange={(e) => setType(e.target.value as Type | "")}
            sx={{ minWidth: 190 }}
          >
            <MenuItem value="">Todos</MenuItem>
            {Object.entries(typeLabel).map(([v, l]) => (
              <MenuItem key={v} value={v}>
                {l}
              </MenuItem>
            ))}
          </TextField>
          <TextField
            select
            label="Status"
            size="small"
            value={status}
            onChange={(e) => setStatus(e.target.value as Status | "")}
            sx={{ minWidth: 190 }}
          >
            <MenuItem value="">Todos</MenuItem>
            {(
              [
                "Submitted",
                "Acknowledged",
                "InProgress",
                "WaitingManager",
                "Completed",
                "Cancelled",
              ] as Status[]
            ).map((v) => (
              <MenuItem key={v} value={v}>
                {statusLabel(v, "GeneralQuestion")}
              </MenuItem>
            ))}
          </TextField>
          <TextField
            type="date"
            label="Data inicial"
            size="small"
            value={from}
            onChange={(e) => setFrom(e.target.value)}
            InputLabelProps={{ shrink: true }}
          />
          <TextField
            type="date"
            label="Data final"
            size="small"
            value={to}
            onChange={(e) => setTo(e.target.value)}
            InputLabelProps={{ shrink: true }}
          />
          {filtered && <Button onClick={clear}>Limpar filtros</Button>}
        </Stack>
        {loading ? (
          <Skeleton variant="rounded" height={240} />
        ) : !data?.items.length ? (
          <EmptyState
            title={
              filtered
                ? "Nenhuma solicitação encontrada com os filtros selecionados."
                : "Você ainda não enviou solicitações para a administradora."
            }
            description={
              filtered
                ? "Revise ou limpe os filtros."
                : "Quando precisar, abra uma nova solicitação."
            }
            action={
              <Button
                onClick={
                  filtered ? clear : () => nav("/management/administrator/new")
                }
              >
                {filtered ? "Limpar filtros" : "Nova solicitação"}
              </Button>
            }
          />
        ) : (
          <Stack spacing={1.2}>
            {data.items.map((item) => (
              <Card key={item.id} variant="outlined">
                <CardActionArea
                  onClick={() => nav(`/management/administrator/${item.id}`)}
                >
                  <CardContent>
                    <Box
                      display="flex"
                      justifyContent="space-between"
                      gap={2}
                      flexWrap="wrap"
                    >
                      <Box>
                        <Typography fontWeight={800}>
                          {item.friendlyIdentifier} · {typeLabel[item.type]}
                        </Typography>
                        <Typography>{item.subject}</Typography>
                        {!activeCondominiumId && (
                          <Typography variant="body2" color="text.secondary">
                            {item.condominiumName}
                          </Typography>
                        )}
                      </Box>
                      <Chip
                        color={
                          item.status === "WaitingManager"
                            ? "warning"
                            : "default"
                        }
                        label={statusLabel(item.status, item.type)}
                      />
                    </Box>
                    <Typography mt={1} variant="caption">
                      Atualizada em{" "}
                      {new Date(item.updatedAt).toLocaleString("pt-BR")}
                    </Typography>
                  </CardContent>
                </CardActionArea>
              </Card>
            ))}
            <Pagination
              page={page}
              count={Math.max(1, Math.ceil(data.total / data.pageSize))}
              onChange={(_, v) => setPage(v)}
              sx={{ alignSelf: "center" }}
            />
          </Stack>
        )}
      </Stack>
    </PageContainer>
  );
}
