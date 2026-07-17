import { useCallback, useEffect, useState } from 'react'
import AddRoundedIcon from '@mui/icons-material/AddRounded'
import SearchRoundedIcon from '@mui/icons-material/SearchRounded'
import { Alert, Box, Button, InputAdornment, List, ListItemButton, ListItemText, MenuItem, Skeleton, Stack, TextField, Typography } from '@mui/material'
import { useNavigate } from 'react-router-dom'
import { EmptyState } from '../components/EmptyState'
import { PageContainer } from '../components/PageContainer'
import { useCondominium } from '../condominiums/CondominiumContext'
import { listBlocks, listUnits } from '../management/api'
import { managementError } from '../management/errors'
import { filterUnits, sortBlocks } from '../management/unitPresentation'
import type { CondominiumBlock, Unit } from '../management/types'

export function ManagementUnitsPage() {
  const { currentCondominium } = useCondominium()
  const navigate = useNavigate()
  const [units, setUnits] = useState<Unit[]>([])
  const [blocks, setBlocks] = useState<CondominiumBlock[]>([])
  const [search, setSearch] = useState('')
  const [blockId, setBlockId] = useState('')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const load = useCallback(async () => {
    const id = currentCondominium?.condominium.id
    if (!id) return
    setLoading(true); setUnits([]); setError('')
    try { const [unitData, blockData] = await Promise.all([listUnits(id), listBlocks(id)]); setUnits(unitData); setBlocks(sortBlocks(blockData)) }
    catch (requestError) { setError(managementError(requestError)) }
    finally { setLoading(false) }
  }, [currentCondominium?.condominium.id])
  useEffect(() => { void load() }, [load])
  const visible = filterUnits(units, search, blockId)
  return <PageContainer>
    <Stack direction={{ xs: 'column', sm: 'row' }} justifyContent="space-between" gap={2}><Box><Typography variant="h1">Unidades</Typography><Typography color="text.secondary">Cadastre e consulte as unidades do condomínio.</Typography></Box><Button variant="contained" startIcon={<AddRoundedIcon />} onClick={() => navigate('/management/units/new')}>Nova unidade</Button></Stack>
    {error && <Alert severity="error" sx={{ mt: 2 }}>{error}</Alert>}
    {loading ? <Skeleton variant="rounded" height={220} sx={{ mt: 3 }} /> : <><Stack direction={{ xs: 'column', sm: 'row' }} gap={1.5} mt={3}><TextField size="small" label="Buscar unidade" value={search} onChange={event => setSearch(event.target.value)} sx={{ flex: 1 }} InputProps={{ startAdornment: <InputAdornment position="start"><SearchRoundedIcon /></InputAdornment> }} />{blocks.length > 0 && <TextField select size="small" label="Bloco" value={blockId} onChange={event => setBlockId(event.target.value)} sx={{ minWidth: 200 }}><MenuItem value="">Todos os blocos</MenuItem>{blocks.map(block => <MenuItem key={block.id} value={block.id}>{block.identifier}</MenuItem>)}</TextField>}</Stack>
      {units.length === 0 ? <EmptyState title="Nenhuma unidade cadastrada." description="Cadastre a primeira unidade para organizar moradores e vínculos." /> : visible.length === 0 ? <EmptyState title="Nenhuma unidade encontrada com os filtros selecionados." description="Revise a busca ou o bloco selecionado." /> : <List sx={{ mt: 2, bgcolor: 'background.paper', borderRadius: 2 }}>{visible.map(unit => <ListItemButton key={unit.id} divider onClick={() => navigate(`/management/units/${unit.id}`)} sx={{ py: 1.5 }}><ListItemText primary={`${unit.identifier}${unit.block ? ` / Bloco ${unit.block}` : ''}`} secondary={`${unit.peopleCount ?? 0} ${(unit.peopleCount ?? 0) === 1 ? 'pessoa vinculada' : 'pessoas vinculadas'}`} primaryTypographyProps={{ fontWeight: 750 }} /></ListItemButton>)}</List>}</>}
  </PageContainer>
}
