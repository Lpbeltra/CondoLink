import { useCallback, useEffect, useState } from 'react'
import { Alert, Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, List, ListItem, ListItemText, MenuItem, Stack, Tab, Tabs, TextField, Typography } from '@mui/material'
import { useNavigate, useParams } from 'react-router-dom'
import { PageContainer } from '../../components/PageContainer'
import { createBlock, createUnit, deleteBlock, deleteUnit, listBlocks, listUnits, updateBlock, updateUnit } from '../../management/api'
import { managementError } from '../../management/errors'
import { sortBlocks } from '../../management/unitPresentation'
import type { CondominiumBlock, Unit } from '../../management/types'

type Edit = { id?: string; identifier: string; blockId?: string | null; kind: 'block' | 'unit' } | null

export function OverwatchCondominiumStructurePage() {
  const { condominiumId = '' } = useParams()
  const navigate = useNavigate()
  const [tab, setTab] = useState<'units' | 'blocks'>('units')
  const [units, setUnits] = useState<Unit[]>([])
  const [blocks, setBlocks] = useState<CondominiumBlock[]>([])
  const [edit, setEdit] = useState<Edit>(null)
  const [removing, setRemoving] = useState<Edit>(null)
  const [error, setError] = useState('')
  const load = useCallback(async () => {
    if (!condominiumId) return
    try {
      const [nextUnits, nextBlocks] = await Promise.all([listUnits(condominiumId), listBlocks(condominiumId)])
      setUnits(nextUnits); setBlocks(sortBlocks(nextBlocks)); setError('')
    } catch (reason) { setError(managementError(reason)) }
  }, [condominiumId])
  useEffect(() => { void load() }, [load])
  const save = async () => {
    if (!edit || !edit.identifier.trim()) return
    try {
      if (edit.kind === 'block') {
        if (edit.id) await updateBlock(condominiumId, edit.id, edit.identifier.trim())
        else await createBlock(condominiumId, edit.identifier.trim())
      } else if (edit.id) {
        await updateUnit(condominiumId, edit.id, { identifier: edit.identifier.trim(), blockId: edit.blockId ?? null, description: null })
      } else await createUnit(condominiumId, { identifier: edit.identifier.trim(), blockId: edit.blockId ?? null, floor: null, description: null })
      setEdit(null); await load()
    } catch (reason) { setError(managementError(reason)) }
  }
  const remove = async () => {
    if (!removing?.id) return
    try {
      if (removing.kind === 'block') await deleteBlock(condominiumId, removing.id)
      else await deleteUnit(condominiumId, removing.id)
      setRemoving(null); await load()
    } catch (reason) { setRemoving(null); setError(managementError(reason)) }
  }
  const items = tab === 'units' ? units : blocks
  return <PageContainer>
    <Button onClick={() => navigate(`/overwatch/condominiums/${condominiumId}`)}>Voltar para condomínio</Button>
    <Stack direction="row" justifyContent="space-between" alignItems="center" mt={2} gap={2}><Typography variant="h1">Estrutura do condomínio</Typography><Button variant="contained" onClick={() => setEdit({ kind: tab === 'units' ? 'unit' : 'block', identifier: '', blockId: null })}>{tab === 'units' ? 'Nova unidade' : 'Novo bloco'}</Button></Stack>
    {error && <Alert severity="error" sx={{ mt: 2 }}>{error}</Alert>}
    <Tabs value={tab} onChange={(_, value) => setTab(value)} sx={{ mt: 2 }}><Tab value="units" label="Unidades" /><Tab value="blocks" label="Blocos" /></Tabs>
    <List>{items.map(item => <ListItem key={item.id} divider secondaryAction={<Stack direction="row"><Button size="small" onClick={() => setEdit(tab === 'units' ? { kind: 'unit', id: item.id, identifier: (item as Unit).identifier, blockId: (item as Unit).blockId } : { kind: 'block', id: item.id, identifier: (item as CondominiumBlock).identifier })}>Editar</Button><Button size="small" color="error" onClick={() => setRemoving({ kind: tab === 'units' ? 'unit' : 'block', id: item.id, identifier: (item as { identifier: string }).identifier })}>Excluir</Button></Stack>}><ListItemText primary={(item as { identifier: string }).identifier} secondary={tab === 'units' ? (item as Unit).block ?? 'Sem bloco' : `${(item as CondominiumBlock).unitCount} unidades`} /></ListItem>)}</List>
    <Dialog open={Boolean(edit)} onClose={() => setEdit(null)} fullWidth maxWidth="xs"><DialogTitle>{edit?.id ? 'Editar' : 'Cadastrar'} {edit?.kind === 'unit' ? 'unidade' : 'bloco'}</DialogTitle><DialogContent><TextField autoFocus fullWidth label="Identificação" value={edit?.identifier ?? ''} onChange={event => setEdit(current => current ? { ...current, identifier: event.target.value } : current)} sx={{ mt: 1 }} />{edit?.kind === 'unit' && <TextField select fullWidth label="Bloco" value={edit.blockId ?? ''} onChange={event => setEdit(current => current ? { ...current, blockId: event.target.value || null } : current)} sx={{ mt: 2 }}><MenuItem value="">Sem bloco</MenuItem>{blocks.map(block => <MenuItem value={block.id} key={block.id}>{block.identifier}</MenuItem>)}</TextField>}</DialogContent><DialogActions><Button onClick={() => setEdit(null)}>Cancelar</Button><Button variant="contained" onClick={() => void save()}>Salvar</Button></DialogActions></Dialog>
    <Dialog open={Boolean(removing)} onClose={() => setRemoving(null)}><DialogTitle>Excluir {removing?.kind === 'unit' ? 'unidade' : 'bloco'}?</DialogTitle><DialogContent>Esta ação não pode ser desfeita.</DialogContent><DialogActions><Button onClick={() => setRemoving(null)}>Cancelar</Button><Button color="error" variant="contained" onClick={() => void remove()}>Excluir</Button></DialogActions></Dialog>
  </PageContainer>
}
