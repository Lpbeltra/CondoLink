import { useEffect, useMemo, useState } from 'react'
import {
  Accordion, AccordionDetails, AccordionSummary, Alert, Button, Chip, CircularProgress,
  Dialog, DialogActions, DialogContent, DialogTitle, FormControl, InputLabel, MenuItem,
  Select, Stack, TextField, ToggleButton, ToggleButtonGroup, Typography,
} from '@mui/material'
import ExpandMoreRoundedIcon from '@mui/icons-material/ExpandMoreRounded'
import { api } from '../../services/api'

type Status = 'New' | 'Existing' | 'Conflict' | 'Ambiguous' | 'NoChange'
type Contact = {
  externalIds: string[]; name: string | null; email: string | null; phones: string[]
  taxIdMasked: string | null; relationships: { externalRelationshipType: string | null; entryDate: string | null; exitDate: string | null }[]
  personStatus: Status; comvyName: string | null; unitRelationshipStatus: Status
}
type Unit = { externalUnitId: string; identifier: string; block: string | null; status: Status; comvyIdentifier: string | null; contacts: Contact[] }
type Block = { identifier: string; status: Status; units: Unit[] }
type Preview = {
  externalCondominiumId: string; externalRecordsRead: number
  summary: { externalRecordsRead: number; blocks: number; units: number; contacts: number; newUnits: number; existingUnits: number; conflicts: number; ambiguous: number }
  blocks: Block[]; unitsWithoutBlock: Unit[]
}
type Filter = 'all' | 'new' | 'existing' | 'conflict' | 'review'

const statusText: Record<Status, string> = { New: 'Novo', Existing: 'Existente', Conflict: 'Conflito', Ambiguous: 'Revisão necessária', NoChange: 'Sem alteração' }
const statusColor = (status: Status): 'success' | 'default' | 'error' | 'warning' | 'info' => status === 'New' ? 'info' : status === 'Existing' || status === 'NoChange' ? 'success' : status === 'Conflict' ? 'error' : 'warning'
const matchesFilter = (unit: Unit, filter: Filter) => filter === 'all'
  || filter === 'new' && (unit.status === 'New' || unit.contacts.some(x => x.personStatus === 'New' || x.unitRelationshipStatus === 'New'))
  || filter === 'existing' && (unit.status === 'Existing' || unit.contacts.some(x => x.personStatus === 'Existing' || x.unitRelationshipStatus === 'NoChange'))
  || filter === 'conflict' && (unit.status === 'Conflict' || unit.contacts.some(x => x.personStatus === 'Conflict'))
  || filter === 'review' && (unit.status === 'Ambiguous' || unit.contacts.some(x => x.personStatus === 'Ambiguous' || x.unitRelationshipStatus === 'Ambiguous'))

export function SuperlogicaImportPreviewDialog({ condominiumId }: { condominiumId: string }) {
  const [open, setOpen] = useState(false)
  const [preview, setPreview] = useState<Preview | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [filter, setFilter] = useState<Filter>('all')
  const [search, setSearch] = useState('')
  const [onlyBlock, setOnlyBlock] = useState('all')

  useEffect(() => {
    if (!open) return
    let stale = false
    setLoading(true); setError(''); setPreview(null)
    void api.get<Preview>(`/overwatch/condominiums/${condominiumId}/integrations/superlogica/import-preview`)
      .then(response => { if (!stale) setPreview(response.data) })
      .catch(requestError => {
        if (stale) return
        const response = (requestError as { response?: { data?: { message?: string; code?: string } } }).response?.data
        setError(response?.message ?? (response?.code === 'invalid_credentials'
          ? 'Credenciais inválidas. Revise a integração Superlógica no Overwatch.'
          : 'Não foi possível concluir a consulta à Superlógica. Tente novamente mais tarde.'))
      })
      .finally(() => { if (!stale) setLoading(false) })
    return () => { stale = true }
  }, [open, condominiumId])

  const searchable = useMemo(() => {
    const term = search.trim().toLocaleLowerCase()
    const filterUnits = (units: Unit[]) => units.filter(unit => matchesFilter(unit, filter) && (!term || [unit.identifier, unit.block, ...unit.contacts.flatMap(contact => [contact.name, contact.email, ...contact.phones])].some(value => value?.toLocaleLowerCase().includes(term))))
    return {
      blocks: (preview?.blocks ?? []).map(block => ({ ...block, units: filterUnits(block.units) })).filter(block => block.units.length > 0),
      unitsWithoutBlock: filterUnits(preview?.unitsWithoutBlock ?? []),
    }
  }, [preview, filter, search])

  const unitView = (unit: Unit) => <Accordion key={unit.externalUnitId} disableGutters>
    <AccordionSummary expandIcon={<ExpandMoreRoundedIcon />}>
      <Stack direction="row" gap={1} alignItems="center" flexWrap="wrap" sx={{ width: '100%' }}>
        <Typography fontWeight={600}>Unidade {unit.identifier}</Typography>
        <Chip size="small" color={statusColor(unit.status)} label={statusText[unit.status]} />
        {unit.comvyIdentifier && <Typography variant="body2" color="text.secondary">Comvy: {unit.comvyIdentifier}</Typography>}
        <Typography variant="caption" color="text.secondary" sx={{ ml: 'auto' }}>#{unit.externalUnitId}</Typography>
      </Stack>
    </AccordionSummary>
    <AccordionDetails>
      {unit.contacts.length === 0 && <Typography color="text.secondary">Nenhum contato retornado.</Typography>}
      <Stack spacing={1.5}>
        {unit.contacts.map((contact, index) => <Stack key={`${unit.externalUnitId}-${contact.externalIds.join('-')}-${index}`} spacing={0.35} sx={{ borderLeft: 1, borderColor: 'divider', pl: 1.5 }}>
          <Stack direction="row" gap={1} alignItems="center" flexWrap="wrap">
            <Typography fontWeight={600}>{contact.name || 'Nome não informado'}</Typography>
            <Chip size="small" variant="outlined" color={statusColor(contact.personStatus)} label={statusText[contact.personStatus]} />
            {contact.comvyName && <Typography variant="body2" color="text.secondary">Comvy: {contact.comvyName}</Typography>}
            {contact.taxIdMasked && <Typography variant="caption" color="text.secondary">CPF: {contact.taxIdMasked}</Typography>}
          </Stack>
          {contact.email && <Typography variant="body2">{contact.email}</Typography>}
          {contact.phones.length > 0 && <Typography variant="body2">{contact.phones.join(' · ')}</Typography>}
          <Stack direction="row" gap={0.75} flexWrap="wrap">
            {contact.relationships.map((relationship, relIndex) => <Typography key={relIndex} variant="caption" color="text.secondary">
              Tipo externo {relationship.externalRelationshipType || 'não informado'}{relationship.entryDate ? ` · entrada ${relationship.entryDate}` : ''}{relationship.exitDate ? ` · saída ${relationship.exitDate}` : ''}
            </Typography>)}
            <Typography variant="caption" color="text.secondary">Vínculo com unidade: {statusText[contact.unitRelationshipStatus]}</Typography>
          </Stack>
          {contact.externalIds.length > 0 && <Typography variant="caption" color="text.secondary">Contato externo #{contact.externalIds.join(', #')}</Typography>}
        </Stack>)}
      </Stack>
    </AccordionDetails>
  </Accordion>

  return <>
    <Button variant="outlined" onClick={() => setOpen(true)}>Visualizar estrutura Superlógica</Button>
    <Dialog open={open} onClose={() => setOpen(false)} fullWidth maxWidth="lg">
      <DialogTitle>Preview de implantação</DialogTitle>
      <DialogContent>
        {loading && <Stack alignItems="center" py={3}><CircularProgress /></Stack>}
        {error && <Alert severity="error" action={<Button onClick={() => { setOpen(false); setTimeout(() => setOpen(true), 0) }}>Tentar novamente</Button>}>{error}</Alert>}
        {preview && <Stack spacing={2}>
          <Typography color="text.secondary">Superlógica #{preview.externalCondominiumId} · leitura concluída</Typography>
          <Stack direction="row" gap={1} flexWrap="wrap">
            <Chip label={`${preview.summary.externalRecordsRead} registros lidos`} />
            <Chip label={`${preview.summary.blocks} blocos`} />
            <Chip label={`${preview.summary.units} unidades únicas`} />
            <Chip label={`${preview.summary.contacts} contatos/vínculos`} />
            <Chip color="info" label={`${preview.summary.newUnits} unidades novas`} />
            <Chip color="success" label={`${preview.summary.existingUnits} existentes`} />
            <Chip color={preview.summary.conflicts ? 'error' : 'default'} label={`${preview.summary.conflicts} conflitos`} />
            <Chip color={preview.summary.ambiguous ? 'warning' : 'default'} label={`${preview.summary.ambiguous} para revisão`} />
          </Stack>
          <Stack direction={{ xs: 'column', sm: 'row' }} gap={1}>
            <TextField label="Buscar unidade, pessoa, e-mail ou telefone" value={search} onChange={event => setSearch(event.target.value)} fullWidth />
            <FormControl sx={{ minWidth: 180 }}><InputLabel id="superlogica-block-filter">Bloco</InputLabel><Select labelId="superlogica-block-filter" label="Bloco" value={onlyBlock} onChange={event => setOnlyBlock(event.target.value)}><MenuItem value="all">Todos os blocos</MenuItem>{preview.blocks.map(block => <MenuItem key={block.identifier} value={block.identifier}>{block.identifier}</MenuItem>)}</Select></FormControl>
          </Stack>
          <ToggleButtonGroup exclusive size="small" value={filter} onChange={(_, value: Filter | null) => value && setFilter(value)} aria-label="Filtrar preview">
            <ToggleButton value="all">Todos</ToggleButton><ToggleButton value="new">Novos</ToggleButton><ToggleButton value="existing">Existentes</ToggleButton><ToggleButton value="conflict">Conflitos</ToggleButton><ToggleButton value="review">Revisão</ToggleButton>
          </ToggleButtonGroup>
          <Stack spacing={1}>
            {searchable.blocks.filter(block => onlyBlock === 'all' || block.identifier === onlyBlock).map(block => <Accordion key={block.identifier} disableGutters>
              <AccordionSummary expandIcon={<ExpandMoreRoundedIcon />}><Stack direction="row" gap={1} alignItems="center"><Typography fontWeight={700}>Bloco {block.identifier}</Typography><Chip size="small" color={statusColor(block.status)} label={statusText[block.status]} /><Typography variant="body2" color="text.secondary">{block.units.length} unidades</Typography></Stack></AccordionSummary>
              <AccordionDetails><Stack spacing={0.5}>{block.units.map(unitView)}</Stack></AccordionDetails>
            </Accordion>)}
            {onlyBlock === 'all' && searchable.unitsWithoutBlock.length > 0 && <Accordion disableGutters>
              <AccordionSummary expandIcon={<ExpandMoreRoundedIcon />}><Typography fontWeight={700}>Unidades sem bloco</Typography></AccordionSummary>
              <AccordionDetails><Stack spacing={0.5}>{searchable.unitsWithoutBlock.map(unitView)}</Stack></AccordionDetails>
            </Accordion>}
            {searchable.blocks.length === 0 && searchable.unitsWithoutBlock.length === 0 && <Typography color="text.secondary">Nenhuma unidade corresponde aos filtros.</Typography>}
          </Stack>
          <Typography variant="caption" color="text.secondary">Códigos de tipo externo preservados sem conversão para papéis Comvy. Preview somente leitura; nada será importado.</Typography>
        </Stack>}
      </DialogContent>
      <DialogActions><Button onClick={() => setOpen(false)}>Fechar</Button></DialogActions>
    </Dialog>
  </>
}
