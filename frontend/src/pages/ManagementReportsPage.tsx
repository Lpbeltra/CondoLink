import { useCallback, useState } from 'react'
import {
  Alert, Box, LinearProgress, Skeleton, ToggleButton, ToggleButtonGroup, Typography,
} from '@mui/material'
import { EmptyState } from '../components/EmptyState'
import { PageContainer } from '../components/PageContainer'
import { PageHeader } from '../components/PageHeader'
import { useGuardedLoad } from '../components/useGuardedLoad'
import { useManagementContext } from '../management/ManagementContext'
import { getRequestReport } from '../reports/api'
import { listManagementRequests } from '../requests/api'
import { describeWindow, formatHours, isEmptyReport, topCategories } from '../reports/presentation'
import { reportWindows, type ReportWindow, type RequestReport } from '../reports/types'
import type { RequestCounts } from '../requests/types'
import { getRequestError } from '../requests/presentation'
import { Link } from 'react-router-dom'

interface OperationalDashboardData {
  counts: RequestCounts
  urgent: number
}

export function ManagementReportsPage() {
  const { activeCondominiumId, activeCondominium, usesConsolidatedManagementScope } = useManagementContext()
  const [days, setDays] = useState<ReportWindow>(30)

  const fetchReport = useCallback(
    () => getRequestReport(days, activeCondominiumId ?? undefined),
    [days, activeCondominiumId],
  )
  const fetchOperational = useCallback(async (): Promise<OperationalDashboardData> => {
    const scope = { condominiumId: activeCondominiumId ?? undefined, pageSize: 1 }
    const [current, urgent] = await Promise.all([
      listManagementRequests(scope),
      listManagementRequests({ ...scope, priority: 'Urgent' }),
    ])
    return { counts: current.counts, urgent: urgent.total }
  }, [activeCondominiumId])
  const { data: report, isLoading: reportLoading, error: reportError } = useGuardedLoad(fetchReport, getRequestError)
  const operational = useGuardedLoad(fetchOperational, getRequestError)

  const currentData = operational.isLoading || operational.error ? null : operational.data
  const historicalReport = reportLoading || reportError ? null : report

  return (
    <PageContainer maxWidth={1200}>
      <PageHeader
        title="Dashboard"
        description={
          usesConsolidatedManagementScope
            ? <>Vis&atilde;o geral dos seus condom&iacute;nios</>
            : <>Vis&atilde;o geral de {activeCondominium?.name ?? 'seu condom&iacute;nio'}</>
        }
      />

      {operational.error && <Alert severity="error" sx={{ mb: 2 }}>{operational.error}</Alert>}
      {reportError && <Alert severity="error" sx={{ mb: 2 }}>{reportError}</Alert>}

      <AttentionSection data={currentData} />
      <OperationSection
        days={days}
        onDaysChange={setDays}
        currentData={currentData}
        report={historicalReport}
        reportLoading={reportLoading}
      />

      {!reportLoading && !reportError && (isEmptyReport(historicalReport) ? (
        <Box mt={3}>
          <EmptyState
            title={'Nenhum novo atendimento neste per' + String.fromCharCode(0xed) + 'odo'}
            description="Os indicadores operacionais acima continuam mostrando o estado atual dos atendimentos."
          />
        </Box>
      ) : historicalReport ? (
        <CategoryPanel report={historicalReport} />
      ) : null)}
    </PageContainer>
  )
}

function AttentionSection({ data }: { data: OperationalDashboardData | null }) {
  const signals = data ? [
    {
      label: 'Dar andamento',
      value: data.counts.waitingForManager,
      description: 'Precisam de acao',
      to: '/management/requests?status=WaitingForManager',
      tone: 'primary.main',
    },
    {
      label: 'Conclus' + String.fromCharCode(0xe3) + 'o pendente',
      value: data.counts.waitingForResidentClosure,
      description: 'Aguardam encerramento',
      to: '/management/requests?status=WaitingForResidentClosure',
      tone: 'warning.main',
    },
    {
      label: 'Urgentes',
      value: data.urgent,
      description: 'Prioridade urgente',
      to: '/management/requests?priority=Urgent',
      tone: 'error.main',
    },
  ] : []

  return (
    <Box component="section" aria-labelledby="attention-heading" mt={1}>
      <Typography id="attention-heading" variant="overline" color="text.secondary" sx={{ fontWeight: 800, letterSpacing: '.1em' }}>
        Precisa da sua aten&ccedil;&atilde;o
      </Typography>
      <Box display="grid" gridTemplateColumns={{ xs: '1fr', sm: 'repeat(3, minmax(0, 1fr))' }} mt={1} borderTop={1} borderBottom={1} borderColor="divider">
        {data ? signals.map((signal) => (
          <Box
            key={signal.label}
            component={Link}
            to={signal.to}
            aria-label={signal.label + ': ' + signal.value + '. ' + signal.description}
            sx={{
              minWidth: 0,
              p: { xs: 1.5, sm: 2 },
              color: 'inherit',
              textDecoration: 'none',
              borderRight: { xs: 0, sm: 1 },
              borderBottom: { xs: 1, sm: 0 },
              borderColor: 'divider',
              '&:last-child': { border: 0 },
              '&:hover': { bgcolor: 'action.hover' },
              '&:focus-visible': { outline: '2px solid', outlineColor: 'primary.main', outlineOffset: '-2px' },
            }}
          >
            <Typography fontWeight={700}>{signal.label}</Typography>
            <Typography variant="h3" mt={0.5} color={signal.tone}>{signal.value}</Typography>
            <Typography color="text.secondary" fontSize=".85rem">{signal.description}</Typography>
          </Box>
        )) : (
          Array.from({ length: 3 }, (_, index) => (
            <Box key={index} p={{ xs: 1.5, sm: 2 }} borderRight={{ xs: 0, sm: index < 2 ? 1 : 0 }} borderColor="divider">
              <Skeleton width="65%" />
              <Skeleton width={48} height={36} />
              <Skeleton width="80%" />
            </Box>
          ))
        )}
      </Box>
    </Box>
  )
}

function OperationSection({
  days, onDaysChange, currentData, report, reportLoading,
}: {
  days: ReportWindow
  onDaysChange: (days: ReportWindow) => void
  currentData: OperationalDashboardData | null
  report: RequestReport | null
  reportLoading: boolean
}) {
  const open = currentData && getOpenRequestCount(currentData.counts)

  return (
    <Box component="section" aria-labelledby="operation-heading" mt={3}>
      <Box display="flex" flexWrap="wrap" alignItems="center" justifyContent="space-between" gap={1}>
        <Typography id="operation-heading" variant="overline" color="text.secondary" sx={{ fontWeight: 800, letterSpacing: '.1em' }}>
          Opera&ccedil;&atilde;o
        </Typography>
        <ToggleButtonGroup
          exclusive
          size="small"
          value={days}
          onChange={(_, value: ReportWindow | null) => value !== null && onDaysChange(value)}
          aria-label="Periodo historico"
        >
          {reportWindows.map((window) => (
            <ToggleButton key={window} value={window} aria-label={window + ' dias'}>{window}d</ToggleButton>
          ))}
        </ToggleButtonGroup>
      </Box>
      <Box display="grid" gridTemplateColumns={{ xs: '1fr', sm: 'repeat(3, minmax(0, 1fr))' }} mt={1} borderTop={1} borderBottom={1} borderColor="divider">
        <OperationMetric label="Em aberto agora" value={open} current />
        <OperationMetric label={'Novos no per' + String.fromCharCode(0xed) + 'odo'} value={report?.summary.total} loading={reportLoading} />
        <OperationMetric label={'1' + String.fromCharCode(0xaa) + ' resposta m' + String.fromCharCode(0xe9) + 'dia'} value={report ? formatHours(report.summary.averageFirstResponseHours) : undefined} loading={reportLoading} />
      </Box>
    </Box>
  )
}

function OperationMetric({
  label, value, loading = false, current = false,
}: {
  label: string
  value: number | string | null | undefined
  loading?: boolean
  current?: boolean
}) {
  return (
    <Box p={{ xs: 1.5, sm: 2 }} borderRight={{ xs: 0, sm: 1 }} borderBottom={{ xs: 1, sm: 0 }} borderColor="divider" sx={{ '&:last-child': { border: 0 } }}>
      <Typography variant="h3">{loading ? <Skeleton width={current ? 48 : 64} /> : value ?? 'â'}</Typography>
      <Typography color="text.secondary" fontSize=".85rem" mt={0.25}>{label}</Typography>
      <Typography color="text.secondary" fontSize=".75rem" mt={0.25}>{current ? 'Estado atual' : 'Periodo selecionado'}</Typography>
    </Box>
  )
}

function getOpenRequestCount(counts: RequestCounts) {
  return counts.open + counts.inProgress + counts.waitingForResident
    + counts.waitingForManager + counts.waitingForThirdParty + counts.waitingForResidentClosure
}

function CategoryPanel({ report }: { report: RequestReport }) {
  const { visible } = topCategories(report, 5)
  const otherTotal = report.byCategory.slice(5).reduce((total, item) => total + item.total, 0)
  const rows = otherTotal > 0 ? [...visible, { categoryId: 'other', name: 'Outros', total: otherTotal }] : visible
  const peak = rows.reduce((max, item) => Math.max(max, item.total), 0)

  return (
    <Box component="section" aria-labelledby="categories-heading" mt={3} mb={2}>
      <Box display="flex" flexWrap="wrap" alignItems="baseline" justifyContent="space-between" gap={1}>
        <Typography id="categories-heading" variant="overline" color="text.secondary" sx={{ fontWeight: 800, letterSpacing: '.1em' }}>
          Principais motivos
        </Typography>
        <Typography color="text.secondary" fontSize=".8rem">{describeWindow(report.period.days)}</Typography>
      </Box>
      <Box mt={1} borderTop={1} borderColor="divider">
        {rows.map((item) => (
          <Box key={item.categoryId} display="grid" gridTemplateColumns="minmax(0, 1fr) auto" alignItems="center" gap={2} py={1.25} borderBottom={1} borderColor="divider">
            <Box minWidth={0}>
              <Typography fontWeight={700}>{item.name}</Typography>
              <LinearProgress
                variant="determinate"
                value={peak === 0 ? 0 : (item.total / peak) * 100}
                aria-label={item.name + ': ' + item.total + ' solicitacoes'}
                sx={{ mt: 0.75, height: 6, borderRadius: 3 }}
              />
            </Box>
            <Typography fontWeight={700} color="text.secondary">{item.total}</Typography>
          </Box>
        ))}
      </Box>
    </Box>
  )
}
