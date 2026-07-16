import { useCallback, useEffect, useState } from 'react'
import AddRoundedIcon from '@mui/icons-material/AddRounded'
import { Alert, Box, Button, Card, CardContent, Chip, Skeleton, Stack, Typography } from '@mui/material'
import { useNavigate } from 'react-router-dom'
import { EmptyState } from '../components/EmptyState'
import { PageContainer } from '../components/PageContainer'
import { useCondominium } from '../condominiums/CondominiumContext'
import { listUnits } from '../management/api'
import { managementError } from '../management/errors'
import type { Unit } from '../management/types'

export function ManagementUnitsPage() {
  const { currentCondominium } = useCondominium(); const navigate = useNavigate(); const [units, setUnits] = useState<Unit[]>([]); const [loading, setLoading] = useState(true); const [error, setError] = useState('')
  const load = useCallback(async () => { const id=currentCondominium?.condominium.id; if(!id)return; setLoading(true);setUnits([]);setError('');try{setUnits(await listUnits(id))}catch(e){setError(managementError(e))}finally{setLoading(false)} },[currentCondominium?.condominium.id])
  useEffect(()=>{void load()},[load])
  return <PageContainer><Stack direction={{xs:'column',sm:'row'}} justifyContent="space-between" gap={2}><Box><Typography variant="h1">Unidades</Typography><Typography color="text.secondary">Cadastre e consulte as unidades do condomínio.</Typography></Box><Button variant="contained" startIcon={<AddRoundedIcon/>} onClick={()=>navigate('/management/units/new')}>Nova unidade</Button></Stack>
    {error&&<Alert severity="error" sx={{mt:2}} action={<Button onClick={()=>void load()}>Tentar novamente</Button>}>{error}</Alert>}
    {loading?<Skeleton variant="rounded" height={220} sx={{mt:3}}/>:units.length===0?<EmptyState title="Nenhuma unidade cadastrada" description="Cadastre a primeira unidade para organizar moradores e vínculos." actionLabel="Cadastrar unidade" onAction={()=>navigate('/management/units/new')}/>:<Box display="grid" gridTemplateColumns={{xs:'1fr',sm:'repeat(2,minmax(0,1fr))',lg:'repeat(3,minmax(0,1fr))'}} gap={2} mt={3}>{units.map(unit=><Card key={unit.id} elevation={0}><CardContent><Stack direction="row" justifyContent="space-between"><Typography variant="h2">{unit.block?`Bloco ${unit.block} · `:''}{unit.identifier}</Typography><Chip size="small" label={unit.isActive?'Ativa':'Inativa'} color={unit.isActive?'success':'default'}/></Stack>{unit.floor&&<Typography color="text.secondary">Andar {unit.floor}</Typography>} {unit.description&&<Typography mt={1} color="text.secondary" sx={{display:'-webkit-box',WebkitLineClamp:2,WebkitBoxOrient:'vertical',overflow:'hidden'}}>{unit.description}</Typography>}<Button sx={{mt:2}} onClick={()=>navigate(`/management/units/${unit.id}`)}>Abrir detalhe</Button></CardContent></Card>)}</Box>}
  </PageContainer>
}
