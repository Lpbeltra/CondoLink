import { useCallback, useEffect, useState } from "react";
import {
  Alert,
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
import { useNavigate } from "react-router-dom";
import { PageContainer } from "../components/PageContainer";
import {
  getAdministratorOptions,
  listAdministratorRequests,
} from "../managementCompanyRequests/api";
import { date, typeLabel } from "../managementCompanyRequests/presentation";
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
  const [page, setPage] = useState(1),
    [search, setSearch] = useState(""),
    [condominiumId, setCondominiumId] = useState(""),
    [categoryId, setCategoryId] = useState(""),
    [status, setStatus] = useState(""),
    [from, setFrom] = useState(""),
    [to, setTo] = useState("");
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
        }),
      );
      setError("");
    } catch {
      setError("Não foi possível carregar as solicitações.");
    } finally {
      setLoading(false);
    }
  }, [page, search, condominiumId, categoryId, status, from, to]);
  useEffect(() => {
    const timer = setTimeout(() => void load(), 300);
    return () => clearTimeout(timer);
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
          <Typography color="text.secondary">
            Acompanhe as solicitações dos condomínios atendidos por{" "}
            {context?.managementCompanyName}.
          </Typography>
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
                onClick={() => nav(`/administrator/requests/${item.id}`)}
              >
                <CardContent>
                  <Stack direction="row" justifyContent="space-between" gap={1}>
                    <div>
                      <Typography fontWeight={800}>
                        {item.friendlyIdentifier}
                      </Typography>
                      <Typography>
                        {typeLabel[item.type]} · {item.condominiumName}
                      </Typography>
                      <Typography color="text.secondary">
                        {item.subject}
                      </Typography>
                      <Typography variant="caption">
                        Aberta em {date(item.createdAt)}
                      </Typography>
                    </div>
                    <Chip
                      color={
                        item.status === "Submitted" ? "primary" : "default"
                      }
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
      </Stack>
    </PageContainer>
  );
}
