import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import SearchRoundedIcon from "@mui/icons-material/SearchRounded";
import {
  Alert,
  Box,
  ButtonBase,
  InputAdornment,
  Skeleton,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import { Link as RouterLink } from "react-router-dom";
import { EmptyState } from "../components/EmptyState";
import { PageContainer } from "../components/PageContainer";
import { useManagementContext } from "../management/ManagementContext";
import { listUnits } from "../management/api";
import { blockLabel, filterUnits } from "../management/components/UnitAutocomplete";
import { managementError } from "../management/errors";
import { naturalCompare } from "../management/unitPresentation";
import type { Unit } from "../management/types";

interface UnitGroup {
  key: string;
  label: string;
  units: Unit[];
}

function groupUnits(units: Unit[], hasBlocks: boolean): UnitGroup[] {
  if (!hasBlocks) return [{ key: "", label: "", units }];

  const byBlock = new Map<string, Unit[]>();
  units.forEach((unit) => {
    const key = unit.block?.trim() ?? "";
    byBlock.set(key, [...(byBlock.get(key) ?? []), unit]);
  });

  return [...byBlock.entries()]
    .sort(([left], [right]) => {
      if (!left) return 1;
      if (!right) return -1;
      return naturalCompare(left, right);
    })
    .map(([key, groupedUnits]) => ({
      key,
      label: key ? blockLabel(key) : "Sem bloco",
      units: groupedUnits,
    }));
}

function occupancyLabel(count: number) {
  if (count === 0) return "Sem moradores";
  return `${count} ${count === 1 ? "morador" : "moradores"}`;
}

export function ManagementUnitsPage() {
  const { activeCondominiumId } = useManagementContext();
  const [units, setUnits] = useState<Unit[]>([]);
  const [search, setSearch] = useState("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const loadVersion = useRef(0);

  const load = useCallback(async () => {
    const version = ++loadVersion.current;
    if (!activeCondominiumId) {
      setUnits([]);
      setLoading(false);
      return;
    }

    setLoading(true);
    setError("");
    try {
      const items = await listUnits(activeCondominiumId);
      if (version === loadVersion.current) setUnits(items);
    } catch (requestError) {
      if (version === loadVersion.current) setError(managementError(requestError));
    } finally {
      if (version === loadVersion.current) setLoading(false);
    }
  }, [activeCondominiumId]);

  useEffect(() => {
    void load();
  }, [load]);

  const visibleUnits = useMemo(
    () => filterUnits(units, search),
    [search, units],
  );
  const hasBlocks = units.some((unit) => Boolean(unit.block?.trim()));
  const groups = useMemo(
    () => groupUnits(visibleUnits, hasBlocks),
    [hasBlocks, visibleUnits],
  );

  if (!activeCondominiumId && !loading) {
    return (
      <PageContainer>
        <Alert severity="info">
          Selecione um condomínio para consultar unidades e vínculos.
        </Alert>
      </PageContainer>
    );
  }

  return (
    <PageContainer>
      <Box>
        <Typography variant="h1">Unidades</Typography>
        <Typography color="text.secondary">
          Consulte unidades e gerencie seus vínculos.
        </Typography>
        {!loading && units.length > 0 && (
          <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
            {units.length} {units.length === 1 ? "unidade" : "unidades"}
          </Typography>
        )}
      </Box>

      {error && <Alert severity="error" sx={{ mt: 2 }}>{error}</Alert>}

      {loading ? (
        <Skeleton variant="rounded" height={220} sx={{ mt: 3 }} />
      ) : (
        <>
          <TextField
            size="small"
            fullWidth
            label="Buscar unidade"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            sx={{ mt: 3, maxWidth: 520 }}
            slotProps={{
              input: {
                startAdornment: (
                  <InputAdornment position="start">
                    <SearchRoundedIcon />
                  </InputAdornment>
                ),
              },
            }}
          />

          {units.length === 0 ? (
            <EmptyState
              title="Nenhuma unidade cadastrada."
              description="As unidades implantadas aparecerão aqui para consulta e gestão de vínculos."
            />
          ) : visibleUnits.length === 0 ? (
            <EmptyState
              title="Nenhuma unidade encontrada."
              description="Revise a busca por unidade ou bloco."
            />
          ) : (
            <Stack gap={3} mt={3}>
              {groups.map((group) => (
                <Box key={group.key || "without-block"}>
                  {hasBlocks && (
                    <Typography variant="subtitle2" color="text.secondary" sx={{ mb: 1 }}>
                      {group.label}
                    </Typography>
                  )}
                  <Box
                    sx={{
                      display: "grid",
                      gridTemplateColumns: {
                        xs: "repeat(2, minmax(0, 1fr))",
                        sm: "repeat(auto-fill, minmax(152px, 1fr))",
                      },
                      gap: 1,
                      maxWidth: 780,
                      "@media (max-width:359px)": {
                        gridTemplateColumns: "minmax(0, 1fr)",
                      },
                    }}
                  >
                    {group.units.map((unit) => (
                      <ButtonBase
                        key={unit.id}
                        component={RouterLink}
                        to={`/management/units/${unit.id}`}
                        sx={{
                          alignItems: "stretch",
                          border: "1px solid",
                          borderColor: "divider",
                          borderRadius: 1.5,
                          color: "text.primary",
                          minHeight: 88,
                          p: 1.5,
                          textAlign: "left",
                          "&:hover, &:focus-visible": {
                            bgcolor: "action.hover",
                            borderColor: "action.active",
                          },
                          "@media (prefers-reduced-motion: no-preference)": {
                            transition: "background-color 150ms ease, border-color 150ms ease",
                          },
                        }}
                      >
                        <Stack justifyContent="space-between" width="100%">
                          <Typography fontWeight={750}>{unit.identifier}</Typography>
                          <Typography variant="body2" color="text.secondary">
                            {occupancyLabel(unit.peopleCount ?? 0)}
                          </Typography>
                        </Stack>
                      </ButtonBase>
                    ))}
                  </Box>
                </Box>
              ))}
            </Stack>
          )}
        </>
      )}
    </PageContainer>
  );
}
