import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react'
import AddRoundedIcon from '@mui/icons-material/AddRounded'
import ContentCopyRoundedIcon from '@mui/icons-material/ContentCopyRounded'
import LockResetRoundedIcon from '@mui/icons-material/LockResetRounded'
import { Alert, Box, Button, Card, CardContent, Checkbox, Chip, Dialog, DialogActions, DialogContent, DialogTitle, FormControlLabel, MenuItem, Skeleton, Stack, TextField, Typography } from '@mui/material'
import { EmptyState } from '../components/EmptyState'
import { PageContainer } from '../components/PageContainer'
import { useManagementContext } from '../management/ManagementContext'
import { listCondominiumMembers, listUnits, onboardMember, resetMemberTemporaryPassword } from '../management/api'
import { managementError } from '../management/errors'
import type { CondominiumMember, RelationshipType, Unit } from '../management/types'
import { formatDateTime } from '../requests/presentation'
import { hasInitialCredentials } from '../management/onboarding'
import { getPersonBadges } from '../management/peoplePresentation'

interface CredentialResult {
  fullName: string
  email: string
  temporaryPassword: string
  reset: boolean
}

const relationshipLabels:Record<RelationshipType,string>={Owner:'Proprietário',Tenant:'Inquilino',AuthorizedOccupant:'Ocupante autorizado'}
const roleLabels:Record<string,string>={Manager:'Síndico / Gestão',Resident:'Morador'}
export function ManagementPeoplePage(){const { activeCondominiumId } = useManagementContext();const[people,setPeople]=useState<CondominiumMember[]>([]);const[units,setUnits]=useState<Unit[]>([]);const[loading,setLoading]=useState(true);const[error,setError]=useState('');const[open,setOpen]=useState(false);const[result,setResult]=useState<CredentialResult|null>(null);const[resetTarget,setResetTarget]=useState<CondominiumMember|null>(null);const[resetting,setResetting]=useState(false);const[copied,setCopied]=useState(false);const[fullName,setFullName]=useState('');const[email,setEmail]=useState('');const[phone,setPhone]=useState('');const[unitId,setUnitId]=useState('');const[type,setType]=useState<RelationshipType>('Owner');const[resident,setResident]=useState(false);const[primary,setPrimary]=useState(false);const[saving,setSaving]=useState(false)
const loadVersion=useRef(0)
const activeIdRef=useRef(activeCondominiumId)
activeIdRef.current=activeCondominiumId
const load = useCallback(async () => {
  const version=++loadVersion.current
  setOpen(false);setUnitId('');setError('');setSaving(false)
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

    if(version!==loadVersion.current)return
    setPeople(peopleData)
    setUnits(unitData.filter((unit) => unit.isActive))
  } catch (requestError) {
    if(version===loadVersion.current)setError(managementError(requestError))
  } finally {
    if(version===loadVersion.current)setLoading(false)
  }
}, [activeCondominiumId])

useEffect(() => {
  void load()
}, [load])

useEffect(() => { setResult(null); setCopied(false) }, [activeCondominiumId])

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

  const operationId = activeCondominiumId
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

    if (activeIdRef.current !== operationId) return

    setOpen(false)
    setFullName('')
    setEmail('')
    setPhone('')
    setUnitId('')
    setResident(false)
    setPrimary(false)
    setResult(hasInitialCredentials(created) ? {
      fullName: created.user.fullName,
      email: created.user.email,
      temporaryPassword: created.initialPassword!,
      reset: false,
    } : null)

    await load()
  } catch (requestError) {
    if (activeIdRef.current === operationId) setError(managementError(requestError))
  } finally {
    if (activeIdRef.current === operationId) setSaving(false)
  }
}

const copy = async () => {
  if (!result) return

  await navigator.clipboard.writeText(
    `CondoLink\n\nE-mail: ${result.email}\nSenha temporária: ${result.temporaryPassword}`,
  )

  setCopied(true)
}

const resetPassword = async () => {
  if (!activeCondominiumId || !resetTarget || resetting) return
  setResetting(true)
  setError('')
  try {
    const reset = await resetMemberTemporaryPassword(
      activeCondominiumId,
      resetTarget.userId,
    )
    setResetTarget(null)
    setResult({
      fullName: reset.fullName,
      email: reset.email,
      temporaryPassword: reset.temporaryPassword,
      reset: true,
    })
    setPeople(current => current.map(person =>
      person.userId === reset.userId
        ? { ...person, mustChangePassword: true }
        : person))
  } catch (requestError) {
    setError(managementError(requestError))
  } finally {
    setResetting(false)
  }
}
 return <PageContainer><Stack direction={{xs:'column',sm:'row'}} justifyContent="space-between" gap={2}><Box><Typography variant="h1">Pessoas</Typography><Typography color="text.secondary">Gerencie quem possui acesso ao condomínio.</Typography></Box><Button variant="contained" startIcon={<AddRoundedIcon/>} onClick={()=>{setError('');setOpen(true)}}>Adicionar pessoa</Button></Stack>{error&&!open&&<Alert severity="error" sx={{mt:2}} action={<Button onClick={()=>void load()}>Tentar novamente</Button>}>{error}</Alert>}{loading?<Skeleton variant="rounded" height={220} sx={{mt:3}}/>:people.length===0?<EmptyState title="Nenhuma pessoa cadastrada" description="Adicione moradores e responsáveis para que possam acessar o CondoLink." actionLabel="Adicionar pessoa" onAction={()=>setOpen(true)}/>:<Box display="grid" gridTemplateColumns={{xs:'1fr',lg:'repeat(2,minmax(0,1fr))'}} gap={2} mt={3}>{people.map(person=><Card key={person.membershipId} elevation={0}><CardContent><Stack direction="row" justifyContent="space-between" gap={1}><Typography variant="h3">{person.fullName}</Typography></Stack><Typography color="text.secondary">{person.email}{person.phoneNumber?` · ${person.phoneNumber}`:''}</Typography><Stack direction="row" gap={.5} flexWrap="wrap" mt={1}>{getPersonBadges(person).map(badge=><Chip key={badge.label} size="small" label={badge.label} color={badge.color}/>)}{person.roles.map(role=><Chip key={role} size="small" label={roleLabels[role]??role}/>)}</Stack><Typography color="text.secondary" fontSize=".78rem" mt={1}>Entrada: {formatDateTime(person.joinedAt)}</Typography><Button sx={{mt:2}} size="small" variant="outlined" startIcon={<LockResetRoundedIcon/>} disabled={!person.userActive} onClick={()=>setResetTarget(person)}>Redefinir senha temporária</Button></CardContent></Card>)}</Box>}
 <Dialog open={open} onClose={()=>!saving&&setOpen(false)} fullWidth maxWidth="sm"><Box component="form" onSubmit={e=>void submit(e)}><DialogTitle>Adicionar pessoa</DialogTitle><DialogContent><Stack gap={2} pt={1}>{error&&<Alert severity="error">{error}</Alert>}<TextField required label="Nome completo" value={fullName} onChange={e=>setFullName(e.target.value)} slotProps={{htmlInput:{maxLength:200}}}/><TextField required type="email" label="E-mail" value={email} onChange={e=>setEmail(e.target.value)} slotProps={{htmlInput:{maxLength:254}}}/><TextField label="Telefone" value={phone} onChange={e=>setPhone(e.target.value)} slotProps={{htmlInput:{maxLength:30}}}/><TextField select label="Associar a uma unidade (opcional)" value={unitId} onChange={e=>{setUnitId(e.target.value);if(!e.target.value){setResident(false);setPrimary(false)}}}><MenuItem value="">Nenhuma unidade</MenuItem>{units.map(unit=><MenuItem key={unit.id} value={unit.id}>{unit.block?`Bloco ${unit.block} · `:''}{unit.identifier}</MenuItem>)}</TextField>{unitId&&<><TextField select label="Tipo de vínculo" value={type} onChange={e=>setType(e.target.value as RelationshipType)}>{Object.entries(relationshipLabels).map(([v,l])=><MenuItem key={v} value={v}>{l}</MenuItem>)}</TextField><FormControlLabel control={<Checkbox checked={resident} onChange={e=>{setResident(e.target.checked);if(!e.target.checked)setPrimary(false)}}/>} label="Reside na unidade"/><FormControlLabel control={<Checkbox checked={primary} onChange={e=>{setPrimary(e.target.checked);if(e.target.checked)setResident(true)}}/>} label="Residência principal"/></>}</Stack></DialogContent><DialogActions><Button onClick={()=>setOpen(false)} disabled={saving}>Cancelar</Button><Button type="submit" variant="contained" disabled={saving||!fullName.trim()||!email.trim()}>{saving?'Criando...':'Criar conta'}</Button></DialogActions></Box></Dialog>
 <Dialog open={Boolean(resetTarget)} onClose={()=>!resetting&&setResetTarget(null)}><DialogTitle>Redefinir senha temporária?</DialogTitle><DialogContent><Typography>Uma nova senha será gerada para {resetTarget?.fullName}. A senha anterior deixará de funcionar imediatamente.</Typography></DialogContent><DialogActions><Button onClick={()=>setResetTarget(null)} disabled={resetting}>Cancelar</Button><Button variant="contained" onClick={()=>void resetPassword()} disabled={resetting}>{resetting?'Gerando...':'Gerar nova senha'}</Button></DialogActions></Dialog>
 <Dialog open={Boolean(result)} onClose={closeResult} fullWidth maxWidth="sm"><DialogTitle>{result?.reset?'Senha temporária regenerada':'Conta criada com sucesso'}</DialogTitle><DialogContent>{result?.reset&&<Alert severity="success" sx={{mb:2}}>Senha temporária regenerada.</Alert>}<Typography>Compartilhe estas credenciais de forma segura. A senha é exibida somente agora.</Typography>{result&&<Card variant="outlined" sx={{mt:2}}><CardContent><Typography fontWeight={800}>{result.fullName}</Typography><Typography>E-mail: {result.email}</Typography><Typography sx={{fontFamily:'monospace',mt:1}}>Senha temporária: {result.temporaryPassword}</Typography></CardContent></Card>}{copied&&<Alert severity="success" sx={{mt:2}}>Credenciais copiadas.</Alert>}</DialogContent><DialogActions><Button startIcon={<ContentCopyRoundedIcon/>} onClick={()=>void copy()}>Copiar credenciais</Button><Button variant="contained" onClick={closeResult}>Concluir</Button></DialogActions></Dialog></PageContainer>}
