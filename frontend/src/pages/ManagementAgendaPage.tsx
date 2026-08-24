import { useCallback, useEffect, useState } from 'react'
import { Alert, Box, Button, Card, CardContent, Chip, Dialog, DialogActions, DialogContent, DialogTitle, Skeleton, Stack, Tab, Tabs, TextField, Typography } from '@mui/material'
import AddRoundedIcon from '@mui/icons-material/AddRounded'
import { useSearchParams } from 'react-router-dom'
import { PageContainer } from '../components/PageContainer'
import { ManagementCondominiumSwitcher } from '../management/components/ManagementCondominiumSwitcher'
import { useManagementContext } from '../management/ManagementContext'
import { AgendaReminderDialog } from '../agenda/AgendaReminderDialog'
import { deleteAgendaReminder, listAgenda } from '../agenda/api'
import type { AgendaReminder } from '../agenda/types'

const recurrence = { None: 'Não repete', Weekly: 'Semanal', Monthly: 'Mensal' }
export function ManagementAgendaPage() {
  const { activeCondominiumId } = useManagementContext(); const [params, setParams] = useSearchParams()
  const [items, setItems] = useState<AgendaReminder[]>([]); const [view, setView] = useState('upcoming'); const [search, setSearch] = useState('')
  const [loading, setLoading] = useState(true); const [error, setError] = useState(''); const [dialog, setDialog] = useState(false); const [editing, setEditing] = useState<AgendaReminder | null>(null); const [deleting, setDeleting] = useState<AgendaReminder | null>(null)
  const load = useCallback(async () => { if (!activeCondominiumId) return; setLoading(true); try { setItems(await listAgenda(activeCondominiumId, view, search)); setError('') } catch { setError('Não foi possível carregar a Agenda.') } finally { setLoading(false) } }, [activeCondominiumId, view, search])
  useEffect(() => { void load() }, [load]); useEffect(() => { if (params.get('requestId')) setDialog(true) }, [params])
  const close = () => { setDialog(false); setEditing(null); if (params.has('requestId')) { params.delete('requestId'); setParams(params, { replace: true }) } }
  return <PageContainer maxWidth={1200}><Stack spacing={2}>
    <Box display="flex" justifyContent="space-between" gap={2} flexWrap="wrap"><Box><Typography variant="h1">Agenda</Typography><Typography color="text.secondary">Lembretes e compromissos operacionais do condomínio.</Typography></Box><Button variant="contained" startIcon={<AddRoundedIcon />} onClick={() => setDialog(true)}>Novo lembrete</Button></Box>
    <ManagementCondominiumSwitcher /><Tabs value={view} onChange={(_, v) => setView(v)} variant="scrollable"><Tab value="upcoming" label="Próximos" /><Tab value="recurring" label="Recorrentes" /><Tab value="past" label="Concluídos" /></Tabs><TextField size="small" label="Buscar por título ou terceiro" value={search} onChange={e => setSearch(e.target.value)} />
    {error && <Alert severity="error">{error}</Alert>}{loading ? <Skeleton height={180} /> : items.length === 0 ? <Alert severity="info">Nenhum lembrete nesta visão.</Alert> : <Stack spacing={1.5}>{items.map(item => <Card key={item.id} variant="outlined"><CardContent><Box display="flex" justifyContent="space-between" gap={2} flexWrap="wrap"><Box><Typography fontWeight={800}>{item.title}</Typography><Typography color="text.secondary">{item.description || 'Sem descrição'}</Typography><Typography mt={1}>{item.nextOccurrenceAtUtc ? new Intl.DateTimeFormat('pt-BR', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(item.nextOccurrenceAtUtc)) : 'Concluído'}</Typography><Typography variant="body2">{item.block ? `${/^bloco /i.test(item.block) ? item.block : `Bloco ${item.block}`} · ` : ''}{item.unitIdentifier ? `Apto ${item.unitIdentifier}` : ''}{item.relatedThirdParty ? `${item.unitIdentifier ? ' · ' : ''}${item.relatedThirdParty}` : ''}</Typography></Box><Stack direction="row" spacing={1} flexWrap="wrap"><Chip label={recurrence[item.recurrenceType]} /><Chip label={`${item.requestCount} atendimento(s)`} /><Chip label={[item.notifyByWhatsApp && 'WhatsApp', item.notifyByEmail && 'E-mail'].filter(Boolean).join(' + ') || 'Somente Agenda'} /></Stack></Box><Box mt={2}><Button onClick={() => { setEditing(item); setDialog(true) }}>Editar</Button><Button color="error" onClick={() => setDeleting(item)}>Excluir</Button></Box></CardContent></Card>)}</Stack>}
  </Stack>{activeCondominiumId && <AgendaReminderDialog open={dialog} condominiumId={activeCondominiumId} reminder={editing} initialRequestId={params.get('requestId') ?? undefined} onClose={close} onSaved={() => { close(); void load() }} />}
  <Dialog open={Boolean(deleting)} onClose={() => setDeleting(null)}><DialogTitle>Excluir lembrete?</DialogTitle><DialogContent>{deleting?.requestCount ? `Este lembrete está vinculado a ${deleting.requestCount} atendimento(s). Ao excluir, os vínculos serão removidos. Os atendimentos não serão excluídos.` : 'Esta ação não pode ser desfeita.'}</DialogContent><DialogActions><Button onClick={() => setDeleting(null)}>Cancelar</Button><Button color="error" onClick={() => { if (!deleting || !activeCondominiumId) return; void deleteAgendaReminder(activeCondominiumId, deleting.id).then(() => { setDeleting(null); void load() }) }}>Excluir</Button></DialogActions></Dialog>
  </PageContainer>
}
