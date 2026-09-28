import { Box, ButtonBase, Skeleton, Typography } from '@mui/material'
import type { RequestStatus } from '../types'

const requestSummaries = [
  ['Abertas', 'open', 'Open'], ['Em andamento', 'inProgress', 'InProgress'], ['Aguardando morador', 'waitingForResident', 'WaitingForResident'], ['Dar andamento', 'waitingForManager', 'WaitingForManager'], ['Aguardando terceiro', 'waitingForThirdParty', 'WaitingForThirdParty'], ['Conclusão pendente', 'waitingForResidentClosure', 'WaitingForResidentClosure'], ['Resolvidas', 'resolved', 'Resolved'], ['Canceladas', 'cancelled', 'Cancelled'],
] as const

type Counts = Record<typeof requestSummaries[number][1], number>

export function OperationalSummary({ counts, status, loading = false, onSelect }: { counts?: Counts; status: string; loading?: boolean; onSelect: (status: RequestStatus) => void }) {
  if (loading) return <Skeleton variant="rounded" height={48} />
  if (!counts) return null
  return <Box display="flex" gap={.5} sx={{ flexWrap: { xs: 'nowrap', md: 'wrap' }, overflowX: { xs: 'auto', md: 'visible' }, pb: { xs: .5, md: 0 } }} role="group" aria-label="Resumo dos atendimentos">
    {requestSummaries.map(([label, key, summaryStatus]) => <ButtonBase key={key} onClick={() => onSelect(summaryStatus)} aria-label={`Filtrar por ${label}`} aria-pressed={status === summaryStatus} sx={{ flex: { xs: '0 0 auto', md: '1 1 100px' }, minHeight: 44, minWidth: { xs: 0, md: 92 }, justifyContent: 'flex-start', textAlign: 'left', px: 1.25, py: .9, border: '1px solid', borderColor: status === summaryStatus ? 'primary.main' : 'divider', borderRadius: 1, bgcolor: status === summaryStatus ? 'action.selected' : 'background.paper', '&:hover': { bgcolor: 'action.hover' }, '&:focus-visible': { outline: 2, outlineColor: 'primary.main', outlineOffset: -2 } }}>
      <Box minWidth={0} sx={{ display: { xs: 'flex', md: 'block' }, alignItems: 'center', gap: 1 }}><Typography color="text.secondary" fontSize={{ xs: '.8125rem', md: '.69rem' }} noWrap>{label}</Typography><Typography fontSize={{ xs: '.875rem', md: '1.1rem' }} lineHeight={1.15} fontWeight={700}>{counts[key]}</Typography></Box>
    </ButtonBase>)}
  </Box>
}
