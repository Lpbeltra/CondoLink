import { Box, Stack, Typography } from "@mui/material";
import { administratorRequestStatusLabel } from "../administrator/presentation";
import { statusLabel } from "./presentation";
import type { RequestDetail } from "./types";
export function ManagementCompanyRequestTimeline({ request, administrator = false }: { request: RequestDetail; administrator?: boolean }) {
  const history = [...request.history].sort((a, b) => b.createdAt.localeCompare(a.createdAt));
  return <Stack component="ol" sx={{ listStyle: "none", p: 0, m: 0 }}>{history.map((item, index) => {
    const title = item.reason || (item.previousStatus === null ? "Solicitação criada" : `Status alterado para ${administrator ? administratorRequestStatusLabel(item.newStatus, request.type) : statusLabel(item.newStatus, request.type)}`);
    return <Box component="li" key={item.id} display="grid" gridTemplateColumns="24px 1fr" gap={1.5}><Box display="flex" flexDirection="column" alignItems="center"><Box width={10} height={10} borderRadius="50%" bgcolor="primary.main" mt={.75} />{index < history.length - 1 && <Box width="2px" flex={1} minHeight={46} bgcolor="divider" />}</Box><Box pb={index < history.length - 1 ? 2.5 : 0}><Typography fontWeight={700}>{title}</Typography><Typography variant="caption" color="text.secondary">{item.changedByName ? `${item.changedByName} · ` : ""}{new Date(item.createdAt).toLocaleString("pt-BR")}</Typography></Box></Box>;
  })}</Stack>;
}
