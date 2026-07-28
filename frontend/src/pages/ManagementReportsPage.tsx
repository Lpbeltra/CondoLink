import { useCallback, useState } from 'react'
import AssessmentRoundedIcon from '@mui/icons-material/AssessmentRounded'
import HourglassTopRoundedIcon from '@mui/icons-material/HourglassTopRounded'
import MarkChatUnreadRoundedIcon from '@mui/icons-material/MarkChatUnreadRounded'
import TaskAltRoundedIcon from '@mui/icons-material/TaskAltRounded'
import TimerRoundedIcon from '@mui/icons-material/TimerRounded'
import {
  Alert, Box, Card, CardContent, Chip, LinearProgress, Skeleton, Stack,
  Tab, Tabs, Tooltip, Typography, alpha,
} from '@mui/material'
import { EmptyState } from '../components/EmptyState'
import { PageContainer } from '../components/PageContainer'
import { useGuardedLoad } from '../components/useGuardedLoad'
import { ManagementCondominiumSwitcher } from '../management/components/ManagementCondominiumSwitcher'
import { useManagementContext } from '../management/ManagementContext'
import { getRequestReport } from '../reports/api'
import {
  describeWindow, formatDayLabel, formatHours, formatPercent, isEmptyReport,
  priorityLabel, toBarHeights, topCategories, usedPriorities,
} from '../reports/presentation'
import { reportWindows, type ReportWindow, type RequestReport } from '../reports/types'
import { getRequestError } from '../requests/presentation'
import { OverwatchMetricCard } from '../overwatch/components/OverwatchMetricCard'
import { useAuth } from '../auth/AuthContext'

export function ManagementReportsPage() {
  const { user } = useAuth()
  const {
    activeCondominiumId,
    activeCondominium,
    usesConsolidatedManagementScope,
  } = useManagementContext()
  const [days, setDays] = useState<ReportWindow>(30)

  // activeCondominiumId is not read here — it scopes the request server-side,
  // so it must still retrigger the fetch when the manager switches condominium.
  const fetchReport = useCallback(
    () => getRequestReport(days),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [days, activeCondominiumId],
  )
  const { data: report, isLoading, error } = useGuardedLoad(fetchReport, getRequestError)

  return (
    <PageContainer maxWidth={1440}>
      <Typography variant="h1">Dashboard</Typography>
      <Typography color="text.secondary" mt={0.5}>
        {usesConsolidatedManagementScope
          ? `Olá, ${user?.fullName.split(' ')[0] ?? ''}. Aqui está um resumo dos seus condomínios.`
          : `Olá, ${user?.fullName.split(' ')[0] ?? ''}. Aqui está o resumo de ${activeCondominium?.name ?? 'seu condomínio'}.`}
      </Typography>

      <Box mt={2} maxWidth={520}><ManagementCondominiumSwitcher /></Box>

      <Tabs
        value={days}
        onChange={(_, value: ReportWindow) => setDays(value)}
        sx={{ mt: 2 }}
        aria-label="Período do relatório"
      >
        {reportWindows.map((window) => (
          <Tab key={window} value={window} label={describeWindow(window)} />
        ))}
      </Tabs>

      {error && <Alert severity="error" sx={{ mt: 3 }}>{error}</Alert>}

      {isLoading ? (
        <Box
          display="grid"
          gridTemplateColumns={{ xs: '1fr', sm: 'repeat(2, 1fr)', lg: 'repeat(5, 1fr)' }}
          gap={2}
          mt={3}
        >
          {Array.from({ length: 5 }, (_, index) => (
            <Skeleton key={index} variant="rounded" height={104} />
          ))}
        </Box>
      ) : isEmptyReport(report) ? (
        <Box mt={3}>
          <EmptyState
            title="Sem dados no período"
            description="Nenhuma solicitação foi aberta no período selecionado. Escolha um período maior para ver os indicadores."
          />
        </Box>
      ) : report && (
        <>
          <Box
            display="grid"
            gridTemplateColumns={{ xs: '1fr', sm: 'repeat(2, 1fr)', lg: 'repeat(5, 1fr)' }}
            gap={2}
            mt={3}
          >
            <OverwatchMetricCard
              label="Solicitações"
              value={report.summary.total}
              icon={<AssessmentRoundedIcon />}
            />
            <OverwatchMetricCard
              label="Em aberto"
              value={report.summary.open}
              icon={<HourglassTopRoundedIcon />}
            />
            <OverwatchMetricCard
              label="Sem resposta"
              value={report.summary.awaitingFirstResponse}
              icon={<MarkChatUnreadRoundedIcon />}
            />
            <OverwatchMetricCard
              label="1ª resposta (média)"
              value={formatHours(report.summary.averageFirstResponseHours)}
              icon={<TimerRoundedIcon />}
            />
            <OverwatchMetricCard
              label="Resolução (média)"
              value={formatHours(report.summary.averageResolutionHours)}
              icon={<TaskAltRoundedIcon />}
            />
          </Box>

          <Box
            display="grid"
            gridTemplateColumns={{ xs: '1fr', lg: '3fr 2fr' }}
            gap={2}
            mt={2}
          >
            <Card elevation={0}>
              <CardContent>
                <Typography variant="h3">Solicitações por dia</Typography>
                <Typography color="text.secondary" fontSize=".85rem" mt={0.5}>
                  {describeWindow(report.period.days)}
                </Typography>
                <DailyChart series={report.createdPerDay} />
              </CardContent>
            </Card>

            <Card elevation={0}>
              <CardContent>
                <Typography variant="h3">Taxa de resolução</Typography>
                <Typography variant="h1" mt={1}>
                  {formatPercent(report.summary.resolutionRatePercent)}
                </Typography>
                <Typography color="text.secondary" fontSize=".85rem" mt={0.5}>
                  Solicitações resolvidas, desconsiderando canceladas.
                </Typography>

                <Stack direction="row" flexWrap="wrap" gap={1} mt={2}>
                  {usedPriorities(report).map((item) => (
                    <Chip
                      key={item.priority}
                      label={`${priorityLabel(item.priority)}: ${item.total}`}
                      size="small"
                      variant="outlined"
                    />
                  ))}
                </Stack>
              </CardContent>
            </Card>
          </Box>

          <CategoryPanel report={report} />
        </>
      )}
    </PageContainer>
  )
}

function DailyChart({ series }: { series: { day: string; created: number }[] }) {
  const heights = toBarHeights(series)
  const peak = series.reduce((max, item) => Math.max(max, item.created), 0)

  return (
    <Box mt={2}>
      {/* Text alternative: a bar chart alone is not screen-reader friendly. */}
      <Box
        role="img"
        aria-label={
          `Solicitações por dia. Pico de ${peak} em um único dia ao longo de ${series.length} dias.`
        }
        display="flex"
        alignItems="flex-end"
        gap={0.5}
        height={140}
        sx={{ overflowX: 'auto' }}
      >
        {series.map((item, index) => (
          <Tooltip key={item.day} title={`${formatDayLabel(item.day)}: ${item.created}`}>
            <Box
              flex="1 0 8px"
              minWidth={8}
              height={`${Math.max(heights[index], 2)}%`}
              borderRadius={1}
              sx={(theme) => ({
                bgcolor: item.created === 0
                  ? alpha(theme.palette.text.secondary, 0.18)
                  : theme.palette.primary.main,
                transition: 'height 200ms ease',
              })}
            />
          </Tooltip>
        ))}
      </Box>
      <Box display="flex" justifyContent="space-between" mt={1}>
        <Typography color="text.secondary" fontSize=".75rem">
          {series.length > 0 && formatDayLabel(series[0].day)}
        </Typography>
        <Typography color="text.secondary" fontSize=".75rem">
          {series.length > 0 && formatDayLabel(series[series.length - 1].day)}
        </Typography>
      </Box>
    </Box>
  )
}

function CategoryPanel({ report }: { report: RequestReport }) {
  const { visible, hidden } = topCategories(report)
  const peak = visible.reduce((max, item) => Math.max(max, item.total), 0)

  return (
    <Card elevation={0} sx={{ mt: 2 }}>
      <CardContent>
        <Typography variant="h3">Solicitações por categoria</Typography>
        <Stack gap={2} mt={2}>
          {visible.map((item) => (
            <Box key={item.categoryId}>
              <Box display="flex" justifyContent="space-between" gap={2} mb={0.5}>
                <Typography fontWeight={700} noWrap>{item.name}</Typography>
                <Typography color="text.secondary" fontSize=".85rem" flexShrink={0}>
                  {item.total} · {item.open} em aberto · {formatHours(item.averageResolutionHours)}
                </Typography>
              </Box>
              <LinearProgress
                variant="determinate"
                value={peak === 0 ? 0 : (item.total / peak) * 100}
                aria-label={`${item.name}: ${item.total} solicitações`}
                sx={{ height: 8, borderRadius: 4 }}
              />
            </Box>
          ))}
        </Stack>
        {hidden > 0 && (
          <Typography color="text.secondary" fontSize=".8rem" mt={2}>
            +{hidden} {hidden === 1 ? 'outra categoria' : 'outras categorias'} não exibidas.
          </Typography>
        )}
      </CardContent>
    </Card>
  )
}
