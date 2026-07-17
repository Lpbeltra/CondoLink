import { useEffect, useState, type FormEvent } from 'react'
import ArrowBackRoundedIcon from '@mui/icons-material/ArrowBackRounded'
import { Alert, Autocomplete, Button, Card, CardContent, Skeleton, Stack, TextField, Typography } from '@mui/material'
import { useNavigate } from 'react-router-dom'
import { PageContainer } from '../components/PageContainer'
import { useCondominium } from '../condominiums/CondominiumContext'
import { createUnit, listBlocks } from '../management/api'
import { managementError } from '../management/errors'
import { sortBlocks } from '../management/unitPresentation'
import type { CondominiumBlock } from '../management/types'

export function CreateUnitPage() {
  const navigate=useNavigate(); const {currentCondominium}=useCondominium(); const [identifier,setIdentifier]=useState(''); const [block,setBlock]=useState<CondominiumBlock|null>(null); const [description,setDescription]=useState(''); const [blocks,setBlocks]=useState<CondominiumBlock[]>([]); const [loading,setLoading]=useState(true); const [saving,setSaving]=useState(false); const [error,setError]=useState(''); const condominiumId=currentCondominium?.condominium.id
  useEffect(()=>{if(!condominiumId)return;setLoading(true);listBlocks(condominiumId).then(data=>setBlocks(sortBlocks(data))).catch(e=>setError(managementError(e))).finally(()=>setLoading(false))},[condominiumId])
  const submit=async(e:FormEvent)=>{e.preventDefault();if(!identifier.trim()||saving||!condominiumId||(blocks.length>0&&!block))return;setSaving(true);setError('');try{const unit=await createUnit(condominiumId,{identifier:identifier.trim(),blockId:block?.id??null,floor:null,description:description.trim()||null});navigate(`/management/units/${unit.id}`,{state:{created:true}})}catch(err){setError(managementError(err))}finally{setSaving(false)}}
  return <PageContainer>
    <Button color="inherit" startIcon={<ArrowBackRoundedIcon/>} onClick={()=>navigate('/management/units')}>Voltar</Button><Typography variant="h1" mt={2}>Nova unidade</Typography>
    <Card elevation={0} sx={{mt:3,maxWidth:720}}><CardContent component="form" onSubmit={e=>void submit(e)} sx={{p:{xs:2.5,sm:4}}}><Stack gap={2}>{error&&<Alert severity="error">{error}</Alert>}{loading?<Skeleton height={150}/>:<><TextField required label="Identificação" value={identifier} onChange={e=>setIdentifier(e.target.value)} slotProps={{htmlInput:{maxLength:50}}}/>{blocks.length>0&&<Autocomplete options={blocks} value={block} onChange={(_,value)=>setBlock(value)} getOptionLabel={option=>option.identifier} isOptionEqualToValue={(option,value)=>option.id===value.id} autoHighlight selectOnFocus renderInput={params=><TextField {...params} required label="Bloco"/>}/>}<TextField label="Observação" multiline minRows={3} value={description} onChange={e=>setDescription(e.target.value)} slotProps={{htmlInput:{maxLength:500}}} helperText={`${description.length}/500`}/><Button type="submit" variant="contained" disabled={saving||!identifier.trim()||(blocks.length>0&&!block)}>{saving?'Salvando...':'Cadastrar unidade'}</Button></>}</Stack></CardContent></Card>
  </PageContainer>
}
