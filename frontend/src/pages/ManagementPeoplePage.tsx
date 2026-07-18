import { useCallback, useEffect, useState, type FormEvent } from 'react'
import AddRoundedIcon from '@mui/icons-material/AddRounded'
import ContentCopyRoundedIcon from '@mui/icons-material/ContentCopyRounded'
import { Alert, Box, Button, Card, CardContent, Checkbox, Chip, Dialog, DialogActions, DialogContent, DialogTitle, FormControlLabel, MenuItem, Skeleton, Stack, TextField, Typography } from '@mui/material'
import { EmptyState } from '../components/EmptyState'
import { PageContainer } from '../components/PageContainer'
import { useManagementContext } from '../management/ManagementContext'
import { listCondominiumMembers, listUnits, onboardMember } from '../management/api'
import { managementError } from '../management/errors'
import type { CondominiumMember, OnboardResult, RelationshipType, Unit } from '../management/types'
import { formatDateTime } from '../requests/presentation'

const relationshipLabels:Record<RelationshipType,string>={Owner:'Proprietário',Tenant:'Inquilino',AuthorizedOccupant:'Ocupante autorizado'}
const roleLabels:Record<string,string>={Manager:'Síndico / Gestão',Resident:'Morador'}
export function ManagementPeoplePage(){const { activeCondominiumId } = useManagementContext();const[people,setPeople]=useState<CondominiumMember[]>([]);const[units,setUnits]=useState<Unit[]>([]);const[loading,setLoading]=useState(true);const[error,setError]=useState('');const[open,setOpen]=useState(false);const[result,setResult]=useState<OnboardResult|null>(null);const[copied,setCopied]=useState(false);const[fullName,setFullName]=useState('');const[email,setEmail]=useState('');const[phone,setPhone]=useState('');const[unitId,setUnitId]=useState('');const[type,setType]=useState<RelationshipType>('Owner');const[resident,setResident]=useState(false);const[primary,setPrimary]=useState(false);const[saving,setSaving]=useState(false)
const load = useCallback(async () => {
  if (!activeCondominiumId) {
    setPeople([])
    setUnits([])
    setLoading(false)
    return
  }

  setLoading(true)
  setPeople([])
  setUnits([])
  setError('')

  try {
    const [peopleData, unitData] = await Promise.all([
      listCondominiumMembers(activeCondominiumId),
      listUnits(activeCondominiumId),
    ])

    setPeople(peopleData)
    setUnits(unitData.filter((unit) => unit.isActive))
  } catch (requestError) {
    setError(managementError(requestError))
  } finally {
    setLoading(false)
  }
}, [activeCondominiumId])

useEffect(() => {
  void load()
}, [load])

if (!activeCondominiumId && !loading) {
  return (
    <PageContainer>
      <Alert severity="info">
        Selecione um condomínio para gerenciar as pessoas.
      </Alert>
    </PageContainer>
  )
}

const closeResult = () => {
  setResult(null)
  setCopied(false)
}

const submit = async (event: FormEvent) => {
  event.preventDefault()

  if (
    !activeCondominiumId ||
    saving ||
    !fullName.trim() ||
    !email.trim()
  ) {
    return
  }

  setSaving(true)
  setError('')

  try {
    const created = await onboardMember(activeCondominiumId, {
      fullName: fullName.trim(),
      email: email.trim(),
      phoneNumber: phone.trim() || null,
      unitId: unitId || null,
      relationshipType: unitId ? type : null,
      isResident: unitId ? resident : false,
      isPrimaryResidence: unitId ? primary : false,
    })

    setOpen(false)
    setFullName('')
    setEmail('')
    setPhone('')
    setUnitId('')
    setResident(false)
    setPrimary(false)
    setResult(created)

    await load()
  } catch (requestError) {
    setError(managementError(requestError))
  } finally {
    setSaving(false)
  }
}

const copy = async () => {
  if (!result) return

  await navigator.clipboard.writeText(
    `CondoLink\n\nE-mail: ${result.user.email}\nSenha inicial: ${result.initialPassword}`,
  )

  setCopied(true)
}
 return <PageContainer><Stack direction={{xs:'column',sm:'row'}} justifyContent="space-between" gap={2}><Box><Typography variant="h1">Pessoas</Typography><Typography color="text.secondary">Gerencie quem possui acesso ao condomínio.</Typography></Box><Button variant="contained" startIcon={<AddRoundedIcon/>} onClick={()=>{setError('');setOpen(true)}}>Adicionar pessoa</Button></Stack>{error&&!open&&<Alert severity="error" sx={{mt:2}} action={<Button onClick={()=>void load()}>Tentar novamente</Button>}>{error}</Alert>}{loading?<Skeleton variant="rounded" height={220} sx={{mt:3}}/>:people.length===0?<EmptyState title="Nenhuma pessoa cadastrada" description="Adicione moradores e responsáveis para que possam acessar o CondoLink." actionLabel="Adicionar pessoa" onAction={()=>setOpen(true)}/>:<Box display="grid" gridTemplateColumns={{xs:'1fr',lg:'repeat(2,minmax(0,1fr))'}} gap={2} mt={3}>{people.map(person=><Card key={person.membershipId} elevation={0}><CardContent><Stack direction="row" justifyContent="space-between" gap={1}><Typography variant="h3">{person.fullName}</Typography><Chip size="small" label={person.membershipActive?'Ativo':'Encerrado'} color={person.membershipActive?'success':'default'}/></Stack><Typography color="text.secondary">{person.email}{person.phoneNumber?` · ${person.phoneNumber}`:''}</Typography><Stack direction="row" gap={.5} flexWrap="wrap" mt={1}>{person.roles.map(role=><Chip key={role} size="small" label={roleLabels[role]??role}/>)}</Stack><Typography color="text.secondary" fontSize=".78rem" mt={1}>Entrada: {formatDateTime(person.joinedAt)}</Typography></CardContent></Card>)}</Box>}
 <Dialog open={open} onClose={()=>!saving&&setOpen(false)} fullWidth maxWidth="sm"><Box component="form" onSubmit={e=>void submit(e)}><DialogTitle>Adicionar pessoa</DialogTitle><DialogContent><Stack gap={2} pt={1}>{error&&<Alert severity="error">{error}</Alert>}<TextField required label="Nome completo" value={fullName} onChange={e=>setFullName(e.target.value)} slotProps={{htmlInput:{maxLength:200}}}/><TextField required type="email" label="E-mail" value={email} onChange={e=>setEmail(e.target.value)} slotProps={{htmlInput:{maxLength:254}}}/><TextField label="Telefone" value={phone} onChange={e=>setPhone(e.target.value)} slotProps={{htmlInput:{maxLength:30}}}/><TextField select label="Associar a uma unidade (opcional)" value={unitId} onChange={e=>{setUnitId(e.target.value);if(!e.target.value){setResident(false);setPrimary(false)}}}><MenuItem value="">Nenhuma unidade</MenuItem>{units.map(unit=><MenuItem key={unit.id} value={unit.id}>{unit.block?`Bloco ${unit.block} · `:''}{unit.identifier}</MenuItem>)}</TextField>{unitId&&<><TextField select label="Tipo de vínculo" value={type} onChange={e=>setType(e.target.value as RelationshipType)}>{Object.entries(relationshipLabels).map(([v,l])=><MenuItem key={v} value={v}>{l}</MenuItem>)}</TextField><FormControlLabel control={<Checkbox checked={resident} onChange={e=>{setResident(e.target.checked);if(!e.target.checked)setPrimary(false)}}/>} label="Reside na unidade"/><FormControlLabel control={<Checkbox checked={primary} onChange={e=>{setPrimary(e.target.checked);if(e.target.checked)setResident(true)}}/>} label="Residência principal"/></>}</Stack></DialogContent><DialogActions><Button onClick={()=>setOpen(false)} disabled={saving}>Cancelar</Button><Button type="submit" variant="contained" disabled={saving||!fullName.trim()||!email.trim()}>{saving?'Criando...':'Criar conta'}</Button></DialogActions></Box></Dialog>
 <Dialog open={Boolean(result)} onClose={closeResult} fullWidth maxWidth="sm"><DialogTitle>Conta criada com sucesso</DialogTitle><DialogContent><Typography>Compartilhe estas credenciais de forma segura com o morador. A senha é exibida somente agora.</Typography>{result&&<Card variant="outlined" sx={{mt:2}}><CardContent><Typography fontWeight={800}>{result.user.fullName}</Typography><Typography>E-mail: {result.user.email}</Typography><Typography sx={{fontFamily:'monospace',mt:1}}>Senha inicial: {result.initialPassword}</Typography></CardContent></Card>}{copied&&<Alert severity="success" sx={{mt:2}}>Credenciais copiadas.</Alert>}</DialogContent><DialogActions><Button startIcon={<ContentCopyRoundedIcon/>} onClick={()=>void copy()}>Copiar credenciais</Button><Button variant="contained" onClick={closeResult}>Concluir</Button></DialogActions></Dialog></PageContainer>}
