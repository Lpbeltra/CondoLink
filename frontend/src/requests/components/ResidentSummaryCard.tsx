import { Card, CardContent, Grid, Typography } from '@mui/material'
import { formatResidentPhone } from '../presentation'
import type { ResidentSummary } from '../types'

const relationshipLabels = {
  Owner: 'Proprietário',
  Tenant: 'Inquilino',
  AuthorizedOccupant: 'Ocupante autorizado',
} as const

export function ResidentSummaryCard({ resident }: { resident: ResidentSummary }) {
  const relationship = resident.relationship
    ? relationshipLabels[resident.relationship] : null
  const fields = [
    ['Nome completo', resident.fullName],
    ...(resident.block ? [['Bloco', resident.block]] : []),
    ['Unidade', resident.unit || 'Não informada'],
    ['Telefone', formatResidentPhone(resident.phoneNumber)],
    ['E-mail', resident.email || 'Não informado'],
    ...(relationship ? [['Relação com a unidade', relationship]] : []),
  ]
  return (
    <Card elevation={0} sx={{ mt: 3 }}>
      <CardContent sx={{ p: { xs: 2.5, sm: 3 } }}>
        <Typography variant="h2" mb={2}>Dados do morador</Typography>
        <Grid container spacing={2}>
          {fields.map(([label, value]) => (
            <Grid key={label} size={{ xs: 12, sm: 6, md: 4 }}>
              <Typography variant="caption" color="text.secondary">{label}</Typography>
              <Typography sx={{ overflowWrap: 'anywhere' }}>{value}</Typography>
            </Grid>
          ))}
        </Grid>
      </CardContent>
    </Card>
  )
}
