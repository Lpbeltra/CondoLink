import { Paper, Stack, Typography } from "@mui/material";
import type { ReactNode } from "react";
export function ManagementCompanyRequestActionCard({ children }: { children: ReactNode }) { return <Paper elevation={0} sx={{ p: { xs: 2.5, sm: 3 }, border: "1px solid", borderColor: "rgba(114,89,217,.25)", bgcolor: "rgba(114,89,217,.035)" }}><Typography variant="h2">Ações de atendimento</Typography><Typography color="text.secondary" mt={.5} mb={2}>Atualize a situação desta solicitação.</Typography><Stack direction="row" gap={1} flexWrap="wrap">{children}</Stack></Paper>; }
