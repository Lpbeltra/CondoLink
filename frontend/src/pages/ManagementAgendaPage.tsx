import { useCallback, useEffect, useMemo, useState } from 'react'
import { Alert, Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, IconButton, Menu, MenuItem, Skeleton, Stack, Tab, Tabs, TextField, Tooltip, Typography } from '@mui/material'
import AddRoundedIcon from '@mui/icons-material/AddRounded'
import CheckRoundedIcon from '@mui/icons-material/CheckRounded'
import MoreVertRoundedIcon from '@mui/icons-material/MoreVertRounded'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { PageContainer } from '../components/PageContainer'
import { PageHeader } from '../components/PageHeader'
import { useGuardedLoad } from '../components/useGuardedLoad'
import { useManagementContext } from '../management/ManagementContext'
import { AgendaReminderDialog } from '../agenda/AgendaReminderDialog'
import { completeAgendaReminder, deleteAgendaReminder, listAgenda, reactivateAgendaReminder } from '../agenda/api'
import { classifyAgendaDate, formatAgendaDay, formatAgendaTime } from '../agenda/time'
import type { AgendaReminder } from '../agenda/types'
import { getRequestError } from '../requests/presentation'

const recurrence = { None: 'N' + String.fromCharCode(0xe3) + 'o repete', Weekly: 'Semanal', Monthly: 'Mensal' }
const tomorrowLabel = 'Amanh' + String.fromCharCode(0xe3)
const nextLabel = 'Pr' + String.fromCharCode(0xf3) + 'ximos'
const completedLabel = 'Conclu' + String.fromCharCode(0xed) + 'do'
function recurrenceLabel(item: AgendaReminder) { return recurrence[item.recurrenceType] }

export function ManagementAgendaPage() {
  const { activeCondominiumId, activeCondominium } = useManagementContext()
  const navigate = useNavigate(); const [params, setParams] = useSearchParams()
  const [view, setView] = useState('upcoming'); const [search, setSearch] = useState('')
  const [dialog, setDialog] = useState(false); const [editing, setEditing] = useState<AgendaReminder | null>(null)
  const [deleting, setDeleting] = useState<AgendaReminder | null>(null); const [menuAnchor, setMenuAnchor] = useState<HTMLElement | null>(null)
  const [menuReminder, setMenuReminder] = useState<AgendaReminder | null>(null); const [reactivateAfterEdit, setReactivateAfterEdit] = useState<string | null>(null)
  const fetchAgenda = useCallback(() => activeCondominiumId ? listAgenda(activeCondominiumId, view, search) : Promise.resolve([]), [activeCondominiumId, search, view])
  const agenda = useGuardedLoad(fetchAgenda, getRequestError)
  const items = useMemo(() => agenda.isLoading || agenda.error ? [] : agenda.data ?? [], [agenda.data, agenda.error, agenda.isLoading])
  const focusedReminderId = params.get('reminderId')

  useEffect(() => {
    if (agenda.isLoading || agenda.error || !focusedReminderId) return
    if (items.some(item => item.id === focusedReminderId)) { document.getElementById('agenda-reminder-' + focusedReminderId)?.scrollIntoView?.({ block: 'nearest' }); return }
    const next = new URLSearchParams(params); next.delete('reminderId'); setParams(next, { replace: true })
  }, [agenda.error, agenda.isLoading, focusedReminderId, items, params, setParams])
  useEffect(() => { if (params.get('create') === 'true' && params.get('requestId')) { setEditing(null); setDialog(true) } }, [params])

  const close = () => { setDialog(false); setEditing(null); setReactivateAfterEdit(null); if (params.has('requestId') || params.has('create')) { const next = new URLSearchParams(params); next.delete('requestId'); next.delete('create'); setParams(next, { replace: true }) } }
  const run = async (action: () => Promise<void>) => { try { await action(); await agenda.reload() } catch (error) { agenda.setError(getRequestError(error, 'N' + String.fromCharCode(0xe3) + 'o foi poss' + String.fromCharCode(0xed) + 'vel concluir a a' + String.fromCharCode(0xe7) + String.fromCharCode(0xe3) + 'o.')) } }
  const reactivate = (item: AgendaReminder) => { if (!activeCondominiumId) return; if (item.recurrenceType === 'None' && new Date(item.startsAtUtc) <= new Date()) { setEditing(item); setReactivateAfterEdit(item.id); setDialog(true); return } void run(() => reactivateAgendaReminder(activeCondominiumId, item.id)) }
  const saved = async () => { if (reactivateAfterEdit && activeCondominiumId) { const id = reactivateAfterEdit; close(); await run(() => reactivateAgendaReminder(activeCondominiumId, id)); return } close(); await agenda.reload() }

  if (!activeCondominiumId) return <PageContainer><Alert severity="info"><Typography fontWeight={800}>Selecione um condom&iacute;nio</Typography>Este m&oacute;dulo trabalha com um condom&iacute;nio por vez. Escolha um condom&iacute;nio no seletor acima para continuar.</Alert></PageContainer>

  const emptyMessage = search.trim() ? 'Nenhum lembrete encontrado para esta busca.' : view === 'upcoming' ? 'Nenhum lembrete ativo.' : view === 'recurring' ? 'Nenhum lembrete recorrente.' : 'Nenhum lembrete ' + completedLabel.toLowerCase() + '.'
  const groups = view === 'upcoming' ? [
    { label: 'Vencidos', items: items.filter(item => item.nextOccurrenceAtUtc && classifyAgendaDate(item.nextOccurrenceAtUtc, item.timeZoneId) === 'past') },
    { label: 'Hoje', items: items.filter(item => item.nextOccurrenceAtUtc && classifyAgendaDate(item.nextOccurrenceAtUtc, item.timeZoneId) === 'today') },
    { label: tomorrowLabel, items: items.filter(item => item.nextOccurrenceAtUtc && classifyAgendaDate(item.nextOccurrenceAtUtc, item.timeZoneId) === 'tomorrow') },
    { label: nextLabel, items: items.filter(item => !item.nextOccurrenceAtUtc || ['future', null].includes(classifyAgendaDate(item.nextOccurrenceAtUtc, item.timeZoneId))) },
  ].filter(group => group.items.length > 0) : [{ label: '', items }]
  const closeMenu = () => { setMenuAnchor(null); setMenuReminder(null) }
  const renderItem = (item: AgendaReminder) => {
    const focused = item.id === focusedReminderId; const bucket = item.nextOccurrenceAtUtc ? classifyAgendaDate(item.nextOccurrenceAtUtc, item.timeZoneId) : null
    const context = [item.block ? (item.block.toLowerCase().startsWith('bloco ') ? item.block : 'Bloco ' + item.block) : '', item.unitIdentifier ? 'Apto ' + item.unitIdentifier : '', item.relatedThirdParty ?? ''].filter(Boolean).join(' · ')
    return <Box key={item.id} id={'agenda-reminder-' + item.id} role="listitem" aria-current={focused ? 'true' : undefined} sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr auto', sm: '112px minmax(0, 1fr) auto' }, gap: { xs: 1, sm: 2 }, alignItems: 'start', py: { xs: 1.75, sm: 1.5 }, px: { xs: 1, sm: 0 }, borderBottom: '1px solid', borderColor: 'divider', bgcolor: focused ? 'action.hover' : 'transparent', borderLeft: focused ? '3px solid' : '3px solid transparent' }}>
      <Box sx={{ gridColumn: { xs: '1 / -1', sm: 'auto' } }}><Typography variant="overline" color={bucket === 'past' && item.isActive ? 'warning.main' : 'text.secondary'} sx={{ fontWeight: 800, letterSpacing: '.08em', lineHeight: 1.2 }}>{item.nextOccurrenceAtUtc ? bucket === 'today' ? 'Hoje' : bucket === 'tomorrow' ? tomorrowLabel : formatAgendaDay(item.nextOccurrenceAtUtc, item.timeZoneId) : 'Sem data'}</Typography><Typography fontWeight={800}>{item.nextOccurrenceAtUtc ? formatAgendaTime(item.nextOccurrenceAtUtc, item.timeZoneId) : '—'}</Typography></Box>
      <Box minWidth={0}><Typography fontWeight={800} sx={{ overflowWrap: 'anywhere' }}>{item.title}</Typography>{item.description && <Typography variant="body2" color="text.secondary" sx={{ display: '-webkit-box', overflow: 'hidden', WebkitBoxOrient: 'vertical', WebkitLineClamp: 2 }}>{item.description}</Typography>}<Stack direction="row" gap={1} flexWrap="wrap" mt={.55}><Typography variant="caption" color="text.secondary">{recurrenceLabel(item)}</Typography>{context && <Typography variant="caption" color="text.secondary">{context}</Typography>}{bucket === 'past' && item.isActive && <Typography variant="caption" color="warning.main" fontWeight={700}>Vencido</Typography>}{!item.isActive && item.completedAt && <Typography variant="caption" color="text.secondary">Conclu&iacute;do em {formatAgendaDay(item.completedAt, item.timeZoneId)} · {formatAgendaTime(item.completedAt, item.timeZoneId)}</Typography>}</Stack>{item.linkedRequests.length > 0 && <Stack direction="row" gap={.5} flexWrap="wrap" mt={.5}>{item.linkedRequests.map(request => <Button key={request.id} size="small" variant="text" onClick={() => navigate('/management/requests/' + request.id)} sx={{ minWidth: 0, p: 0, justifyContent: 'flex-start', fontSize: '.78rem' }}>Atendimento #{request.protocol}</Button>)}</Stack>}</Box>
      <Stack direction="row" alignItems="center" justifyContent="flex-end" sx={{ gridColumn: { xs: '2', sm: 'auto' }, gridRow: { xs: '1 / span 2', sm: 'auto' } }}>{item.isActive ? <Tooltip title="Concluir"><IconButton color="success" aria-label={`Concluir ${item.title}`} onClick={() => void run(() => completeAgendaReminder(activeCondominiumId, item.id))}><CheckRoundedIcon /></IconButton></Tooltip> : <Button size="small" color="success" onClick={() => reactivate(item)}>Reativar</Button>}<Tooltip title="Mais acoes"><IconButton aria-label={`Mais acoes de ${item.title}`} onClick={event => { setMenuAnchor(event.currentTarget); setMenuReminder(item) }}><MoreVertRoundedIcon /></IconButton></Tooltip></Stack>
    </Box>
  }

  return <PageContainer maxWidth={1100}><PageHeader title="Agenda" description={`Lembretes operacionais do ${activeCondominium?.name ?? 'condomínio'}`} actions={<Button variant="contained" startIcon={<AddRoundedIcon />} onClick={() => { setEditing(null); setDialog(true) }}>Novo lembrete</Button>} /><Stack spacing={{ xs: 1.5, md: 2.5 }}><Tabs value={view} onChange={(_, value) => setView(value)} variant="scrollable" sx={{ minHeight: 40, borderBottom: '1px solid', borderColor: 'divider' }}><Tab value="upcoming" label="Ativos" /><Tab value="recurring" label="Recorrentes" /><Tab value="past" label={completedLabel} /></Tabs><TextField size="small" label="Buscar lembrete" placeholder="Título ou terceiro" value={search} onChange={event => setSearch(event.target.value)} sx={{ maxWidth: { sm: 420 } }} />{agenda.error ? <Alert severity="error">{agenda.error}</Alert> : agenda.isLoading ? <Skeleton variant="rounded" height={150} /> : items.length === 0 ? <Alert severity="info" action={view === 'upcoming' && !search.trim() ? <Button color="inherit" size="small" onClick={() => { setEditing(null); setDialog(true) }}>Criar lembrete</Button> : undefined}>{emptyMessage}</Alert> : <Box role="list" sx={{ borderTop: '1px solid', borderColor: 'divider' }}>{groups.map(group => <Box key={group.label} role="group" aria-label={group.label || undefined}>{group.label && <Typography variant="overline" color="text.secondary" sx={{ display: 'block', mt: 2, fontWeight: 800, letterSpacing: '.1em' }}>{group.label}</Typography>}{group.items.map(renderItem)}</Box>)}</Box>}</Stack><Menu anchorEl={menuAnchor} open={Boolean(menuAnchor)} onClose={closeMenu}>{menuReminder?.isActive && <MenuItem onClick={() => { setEditing(menuReminder); setDialog(true); closeMenu() }}>Editar</MenuItem>}<MenuItem onClick={() => { setDeleting(menuReminder); closeMenu() }}>Excluir</MenuItem></Menu><AgendaReminderDialog open={dialog} condominiumId={activeCondominiumId} reminder={editing} initialRequestId={params.get('requestId') ?? undefined} onClose={close} onSaved={() => void saved()} /><Dialog open={Boolean(deleting)} onClose={() => setDeleting(null)}><DialogTitle>Excluir lembrete?</DialogTitle><DialogContent>{deleting?.requestCount ? 'Este lembrete est' + String.fromCharCode(0xe1) + ' vinculado a ' + deleting.requestCount + ' atendimento(s). Ao excluir, os v' + String.fromCharCode(0xed) + 'nculos ser' + String.fromCharCode(0xe3) + 'o removidos. Os atendimentos n' + String.fromCharCode(0xe3) + 'o ser' + String.fromCharCode(0xe3) + 'o exclu' + String.fromCharCode(0xed) + 'dos.' : 'Esta a' + String.fromCharCode(0xe7) + String.fromCharCode(0xe3) + 'o n' + String.fromCharCode(0xe3) + 'o pode ser desfeita.'}</DialogContent><DialogActions><Button onClick={() => setDeleting(null)}>Cancelar</Button><Button color="error" onClick={() => { if (!deleting) return; void run(() => deleteAgendaReminder(activeCondominiumId, deleting.id)).then(() => setDeleting(null)) }}>Excluir</Button></DialogActions></Dialog></PageContainer>
}
