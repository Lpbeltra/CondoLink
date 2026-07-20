import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react'
import AddRoundedIcon from '@mui/icons-material/AddRounded'
import DeleteOutlineRoundedIcon from '@mui/icons-material/DeleteOutlineRounded'
import EditRoundedIcon from '@mui/icons-material/EditRounded'
import { Alert, Box, Button, CircularProgress, Dialog, DialogActions, DialogContent, DialogTitle, IconButton, List, ListItem, ListItemText, Skeleton, Stack, TextField, Typography } from '@mui/material'
import { EmptyState } from '../components/EmptyState'
import { PageContainer } from '../components/PageContainer'
import { useManagementContext } from '../management/ManagementContext'
import { createBlock, deleteBlock, listBlocks, updateBlock } from '../management/api'
import { managementError } from '../management/errors'
import { sortBlocks } from '../management/unitPresentation'
import type { CondominiumBlock } from '../management/types'

export function ManagementBlocksPage() {
  const { activeCondominiumId } = useManagementContext()
  const condominiumId = activeCondominiumId
  const [blocks,setBlocks]=useState<CondominiumBlock[]>([]); const [loading,setLoading]=useState(true); const [saving,setSaving]=useState(false); const [error,setError]=useState(''); const [success,setSuccess]=useState(''); const [editing,setEditing]=useState<CondominiumBlock|null|undefined>(undefined); const [identifier,setIdentifier]=useState(''); const [deleting,setDeleting]=useState<CondominiumBlock|null>(null)
  const loadVersion=useRef(0)
  const activeIdRef=useRef(condominiumId)
  activeIdRef.current=condominiumId
  const load=useCallback(async()=>{const version=++loadVersion.current;setBlocks([]);setEditing(undefined);setDeleting(null);setSuccess('');if (!condominiumId) {
  setBlocks([])
  setLoading(false)
  return};setLoading(true);setError('');try{const result=await listBlocks(condominiumId);if(version===loadVersion.current)setBlocks(sortBlocks(result))}catch(e){if(version===loadVersion.current)setError(managementError(e))}finally{if(version===loadVersion.current)setLoading(false)}},[condominiumId])
  useEffect(()=>{void load()},[load])
  if (!activeCondominiumId && !loading) {
    return (
      <PageContainer>
        <Alert severity="info">
          Selecione um condomínio para consultar e cadastrar blocos.
        </Alert>
      </PageContainer>
    )
  }
  const save=async(e:FormEvent)=>{e.preventDefault();if(!condominiumId||!identifier.trim()||saving)return;const operationId=condominiumId;setSaving(true);setError('');try{if(editing)await updateBlock(condominiumId,editing.id,identifier.trim());else await createBlock(condominiumId,identifier.trim());if(activeIdRef.current!==operationId)return;setEditing(undefined);setIdentifier('');setSuccess(editing?'Bloco atualizado com sucesso.':'Bloco cadastrado com sucesso.');await load()}catch(err){if(activeIdRef.current===operationId)setError(managementError(err))}finally{if(activeIdRef.current===operationId)setSaving(false)}}
  const remove=async()=>{if(!condominiumId||!deleting||saving)return;const operationId=condominiumId;setSaving(true);setError('');try{await deleteBlock(condominiumId,deleting.id);if(activeIdRef.current!==operationId)return;setDeleting(null);setSuccess('Bloco excluído com sucesso.');await load()}catch(err){if(activeIdRef.current===operationId){setDeleting(null);setError(managementError(err))}}finally{if(activeIdRef.current===operationId)setSaving(false)}}
  return <PageContainer>
    <Stack direction={{xs:'column',sm:'row'}} justifyContent="space-between" gap={2}><Box><Typography variant="h1">Blocos</Typography><Typography color="text.secondary">Organize os blocos ou torres do condomínio.</Typography></Box><Button variant="contained" startIcon={<AddRoundedIcon/>} onClick={()=>{setEditing(null);setIdentifier('')}}>Novo bloco</Button></Stack>
    {success&&<Alert severity="success" sx={{mt:2}}>{success}</Alert>}{error&&<Alert severity="error" sx={{mt:2}}>{error}</Alert>}
    {loading?<Skeleton variant="rounded" height={180} sx={{mt:3}}/>:blocks.length===0?<EmptyState title="Nenhum bloco cadastrado." description="Cadastre blocos somente se este condomínio utilizar essa organização."/>:<List sx={{mt:3,bgcolor:'background.paper',borderRadius:2}}>{blocks.map(block=><ListItem key={block.id} divider secondaryAction={<Stack direction="row"><IconButton aria-label={`Editar ${block.identifier}`} onClick={()=>{setEditing(block);setIdentifier(block.identifier)}}><EditRoundedIcon/></IconButton><IconButton aria-label={`Excluir ${block.identifier}`} color="error" onClick={()=>setDeleting(block)}><DeleteOutlineRoundedIcon/></IconButton></Stack>}><ListItemText primary={block.identifier} secondary={`${block.unitCount} ${block.unitCount===1?'unidade vinculada':'unidades vinculadas'}`}/></ListItem>)}</List>}
    <Dialog open={editing!==undefined} onClose={()=>!saving&&setEditing(undefined)} fullWidth maxWidth="xs"><Box component="form" onSubmit={e=>void save(e)}><DialogTitle>{editing?'Editar bloco':'Novo bloco'}</DialogTitle><DialogContent><TextField autoFocus required fullWidth label="Identificação" value={identifier} onChange={e=>setIdentifier(e.target.value)} slotProps={{htmlInput:{maxLength:50}}} sx={{mt:1}}/></DialogContent><DialogActions><Button onClick={()=>setEditing(undefined)} disabled={saving}>Cancelar</Button><Button type="submit" variant="contained" disabled={saving||!identifier.trim()}>{saving?<CircularProgress size={20}/>: 'Salvar'}</Button></DialogActions></Box></Dialog>
    <Dialog open={Boolean(deleting)} onClose={()=>!saving&&setDeleting(null)}><DialogTitle>Excluir bloco</DialogTitle><DialogContent><Typography>Deseja excluir o bloco {deleting?.identifier}?</Typography></DialogContent><DialogActions><Button onClick={()=>setDeleting(null)} disabled={saving}>Voltar</Button><Button color="error" variant="contained" onClick={()=>void remove()} disabled={saving}>{saving?<CircularProgress size={20}/>: 'Excluir'}</Button></DialogActions></Dialog>
  </PageContainer>
}
