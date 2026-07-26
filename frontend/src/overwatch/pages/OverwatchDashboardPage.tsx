import ApartmentRoundedIcon from '@mui/icons-material/ApartmentRounded'
import BusinessRoundedIcon from '@mui/icons-material/BusinessRounded'
import BadgeRoundedIcon from '@mui/icons-material/BadgeRounded'
import SupervisorAccountRoundedIcon from '@mui/icons-material/SupervisorAccountRounded'
import ArrowForwardRoundedIcon from '@mui/icons-material/ArrowForwardRounded'
import { Button, Card, CardContent, Grid, Stack, Typography } from '@mui/material'
import { Link as RouterLink } from 'react-router-dom'
import { PageContainer } from '../../components/PageContainer'
import { OverwatchMetricCard } from '../components/OverwatchMetricCard'
import {
  overwatchMetricLabels,
  overwatchShortcuts,
} from '../dashboard'

const icons = [
  <ApartmentRoundedIcon />,
  <BusinessRoundedIcon />,
  <SupervisorAccountRoundedIcon />,
  <BadgeRoundedIcon />,
]

export function OverwatchDashboardPage() {
  return (
    <PageContainer>
      <Typography variant="h1">Overwatch</Typography>
      <Typography color="text.secondary" mt={1}>
        Visão geral da operação do CondoLink.
      </Typography>
      <Typography color="text.secondary" fontSize=".85rem" mt={2}>
        As métricas consolidadas ainda não estão disponíveis.
      </Typography>

      <Grid container spacing={2} mt={1}>
        {overwatchMetricLabels.map((label, index) => (
          <Grid key={label} size={{ xs: 12, sm: 6, lg: 3 }}>
            <OverwatchMetricCard
              label={label}
              value="Não disponível"
              icon={icons[index]}
            />
          </Grid>
        ))}
      </Grid>

      <Card elevation={0} sx={{ mt: 3 }}>
        <CardContent sx={{ p: { xs: 3, sm: 4 } }}>
          <Typography variant="h2">Bem-vindo ao Overwatch</Typography>
          <Typography color="text.secondary" mt={1}>
            Este painel é responsável pela administração global da plataforma,
            reunindo o acesso às estruturas centrais do CondoLink.
          </Typography>
          <Stack direction={{ xs: 'column', sm: 'row' }} gap={1.5} mt={3}>
            {overwatchShortcuts.map((shortcut) => (
              <Button
                key={shortcut.path}
                component={RouterLink}
                to={shortcut.path}
                variant="outlined"
                endIcon={<ArrowForwardRoundedIcon />}
              >
                {shortcut.label}
              </Button>
            ))}
          </Stack>
        </CardContent>
      </Card>
    </PageContainer>
  )
}
