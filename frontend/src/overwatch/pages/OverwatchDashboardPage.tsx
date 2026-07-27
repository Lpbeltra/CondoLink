import { useCallback, useEffect, useState } from 'react'
import ApartmentRoundedIcon from '@mui/icons-material/ApartmentRounded'
import BusinessRoundedIcon from '@mui/icons-material/BusinessRounded'
import BadgeRoundedIcon from '@mui/icons-material/BadgeRounded'
import SupervisorAccountRoundedIcon from '@mui/icons-material/SupervisorAccountRounded'
import ArrowForwardRoundedIcon from '@mui/icons-material/ArrowForwardRounded'
import { Alert, Button, Card, CardContent, Grid, Stack, Typography } from '@mui/material'
import { Link as RouterLink } from 'react-router-dom'
import { PageContainer } from '../../components/PageContainer'
import { api } from '../../services/api'
import { OverwatchMetricCard } from '../components/OverwatchMetricCard'
import {
  overwatchMetricKeys,
  overwatchMetricLabels,
  overwatchShortcuts,
  type OverwatchDashboardMetrics,
} from '../dashboard'

const icons = [
  <ApartmentRoundedIcon />,
  <BusinessRoundedIcon />,
  <SupervisorAccountRoundedIcon />,
  <BadgeRoundedIcon />,
]

export function OverwatchDashboardPage() {
  const [metrics, setMetrics] = useState<OverwatchDashboardMetrics | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState('')

  const load = useCallback(async () => {
    setIsLoading(true)
    setError('')
    try {
      setMetrics((await api.get<OverwatchDashboardMetrics>('/overwatch/dashboard')).data)
    } catch {
      setMetrics(null)
      setError('Não foi possível carregar as métricas do dashboard.')
    } finally {
      setIsLoading(false)
    }
  }, [])

  useEffect(() => { void load() }, [load])

  return (
    <PageContainer>
      <Typography variant="h1">Overwatch</Typography>
      <Typography color="text.secondary" mt={1}>
        Visão geral da operação do CondoLink.
      </Typography>

      {error && (
        <Alert severity="error" sx={{ mt: 2 }}
          action={<Button color="inherit" onClick={() => void load()}>Tentar novamente</Button>}>
          {error}
        </Alert>
      )}

      <Grid container spacing={2} mt={1}>
        {overwatchMetricLabels.map((label, index) => (
          <Grid key={label} size={{ xs: 12, sm: 6, lg: 3 }}>
            <OverwatchMetricCard
              label={label}
              value={metrics?.[overwatchMetricKeys[index]] ?? 'Indisponível'}
              icon={icons[index]}
              isLoading={isLoading}
            />
          </Grid>
        ))}
      </Grid>

      <Card elevation={0} sx={{ mt: 3 }}>
        <CardContent sx={{ p: { xs: 3, sm: 4 } }}>
          <Typography variant="h2">Bem-vindo ao Overwatch</Typography>
          <Typography color="text.secondary" mt={1}>
            Este painel reúne a administração global das estruturas centrais do CondoLink.
          </Typography>
          <Stack direction={{ xs: 'column', sm: 'row' }} gap={1.5} mt={3}>
            {overwatchShortcuts.map((shortcut) => (
              <Button key={shortcut.path} component={RouterLink} to={shortcut.path}
                variant="outlined" endIcon={<ArrowForwardRoundedIcon />}>
                {shortcut.label}
              </Button>
            ))}
          </Stack>
        </CardContent>
      </Card>
    </PageContainer>
  )
}
