import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react'
import AddRoundedIcon from '@mui/icons-material/AddRounded'
import ContentCopyRoundedIcon from '@mui/icons-material/ContentCopyRounded'
import LockResetRoundedIcon from '@mui/icons-material/LockResetRounded'
import EditRoundedIcon from '@mui/icons-material/EditRounded'
import { Alert, Box, Button, Card, CardContent, Checkbox, Chip, Dialog, DialogActions, DialogContent, DialogTitle, FormControlLabel, MenuItem, Skeleton, Stack, TextField, Typography } from '@mui/material'
import { EmptyState } from '../components/EmptyState'
import { PageContainer } from '../components/PageContainer'
import { useManagementContext } from '../management/ManagementContext'
import { listCondominiumMembers, listUnits, onboardMember, resetMemberTemporaryPassword, updateCondominiumMember } from '../management/api'
import { managementError } from '../management/errors'
import type { CondominiumMember, RelationshipType, Unit } from '../management/types'
import { formatDateTime } from '../requests/presentation'
import { hasInitialCredentials } from '../management/onboarding'
import { getPersonBadges } from '../management/peoplePresentation'
import { temporaryCredentialsWhatsAppText } from '../auth/temporaryCredentials'

interface CredentialResult {
  fullName: string
  email: string
  temporaryPassword: string
  reset: boolean
}

const relationshipLabels:Record<RelationshipType,string>={Owner:'Proprietário',Tenant:'Inquilino',AuthorizedOccupant:'Ocupante autorizado'}
const roleLabels:Record<string,string>={Manager:'Síndico / Gestão',Resident:'Morador'}
export function ManagementPeoplePage(){const { activeCondominiumId } = useManagementContext();const[people,setPeople]=useState<CondominiumMember[]>([]);const[units,setUnits]=useState<Unit[]>([]);const[loading,setLoading]=useState(true);const[error,setError]=useState('');const[open,setOpen]=useState(false);const[result,setResult]=useState<CredentialResult|null>(null);const[resetTarget,setResetTarget]=useState<CondominiumMember|null>(null);const[resetting,setResetting]=useState(false);const[copyFeedback,setCopyFeedback]=useState<{message:string;error:boolean}|null>(null);const[fullName,setFullName]=useState('');const[email,setEmail]=useState('');const[phone,setPhone]=useState('');const[unitId,setUnitId]=useState('');const[type,setType]=useState<RelationshipType>('Owner');const[resident,setResident]=useState(false);const[primary,setPrimary]=useState(false);const[saving,setSaving]=useState(false)
const[editing,setEditing]=useState<CondominiumMember|null>(null)
const[cpf,setCpf]=useState('');const[cnpj,setCnpj]=useState('');const[address,setAddress]=useState('');const[city,setCity]=useState('');const[state,setState]=useState('');const[membershipActive,setMembershipActive]=useState(true);const[unitMembershipId,setUnitMembershipId]=useState<string|null>(null);const[success,setSuccess]=useState('')
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

useEffect(() => { setResult(null); setCopyFeedback(null) }, [activeCondominiumId])

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
  setCopyFeedback(null)
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
    if (editing) {
      const updated = await updateCondominiumMember(
        activeCondominiumId,
        editing.userId,
        {
          fullName: fullName.trim(),
          email: email.trim(),
          phoneNumber: phone.trim() || null,
          cpf: cpf.trim() || null,
          cnpj: cnpj.trim() || null,
          address: address.trim() || null,
          city: city.trim() || null,
          state: state.trim() || null,
          membershipActive,
          unitMembershipId,
          unitId: unitId || null,
          relationshipType: unitId ? type : null,
          isResident: unitId ? resident : false,
          isPrimaryResidence: unitId ? primary : false,
        },
      )
      if (activeIdRef.current !== operationId) return
      setPeople(current => current.map(person =>
        person.userId === updated.userId
          ? {
              ...person,
              fullName: updated.fullName,
              email: updated.email,
              phoneNumber: updated.phoneNumber,
              cpf: updated.cpf,
              cnpj: updated.cnpj,
              address: updated.address,
              city: updated.city,
              state: updated.state,
              membershipActive: updated.membershipActive,
              unitLinks: updated.unitLink ? [updated.unitLink] : [],
            }
          : person))
      setOpen(false)
      setEditing(null)
      setSuccess('Pessoa atualizada com sucesso.')
      return
    }
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

const beginAdd=()=>{setError('');setSuccess('');setEditing(null);setFullName('');setEmail('');setPhone('');setCpf('');setCnpj('');setAddress('');setCity('');setState('');setMembershipActive(true);setUnitMembershipId(null);setUnitId('');setType('Owner');setResident(false);setPrimary(false);setOpen(true)}
const beginEdit=(person:CondominiumMember)=>{const link=person.unitLinks[0]??null;setError('');setSuccess('');setEditing(person);setFullName(person.fullName);setEmail(person.email);setPhone(person.phoneNumber??'');setCpf(person.cpf??'');setCnpj(person.cnpj??'');setAddress(person.address??'');setCity(person.city??'');setState(person.state??'');setMembershipActive(person.membershipActive);setUnitMembershipId(link?.unitMembershipId??null);setUnitId(link?.unitId??'');setType(link?.relationshipType??'Owner');setResident(link?.isResident??false);setPrimary(link?.isPrimaryResidence??false);setOpen(true)}

const copy = async (value: string, message: string) => {
  if (!result) return
  try {
    await navigator.clipboard.writeText(value)
    setCopyFeedback({ message, error: false })
  } catch {
    setCopyFeedback({
      message: 'Não foi possível copiar. Selecione o conteúdo manualmente.',
      error: true,
    })
  }
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
 return <PageContainer><Stack direction={{xs:'column',sm:'row'}} justifyContent="space-between" gap={2}><Box><Typography variant="h1">Pessoas</Typography><Typography color="text.secondary">Gerencie quem possui acesso ao condomínio.</Typography></Box><Button variant="contained" startIcon={<AddRoundedIcon/>} onClick={beginAdd}>Adicionar pessoa</Button></Stack>{success&&<Alert severity="success" sx={{mt:2}}>{success}</Alert>}{error&&!open&&<Alert severity="error" sx={{mt:2}} action={<Button onClick={()=>void load()}>Tentar novamente</Button>}>{error}</Alert>}{loading?<Skeleton variant="rounded" height={220} sx={{mt:3}}/>:people.length===0?<EmptyState title="Nenhuma pessoa cadastrada" description="Adicione moradores e responsáveis para que possam acessar o Comvy." actionLabel="Adicionar pessoa" onAction={beginAdd}/>:<Box display="grid" gridTemplateColumns={{xs:'1fr',lg:'repeat(2,minmax(0,1fr))'}} gap={2} mt={3}>{people.map(person=><Card key={person.membershipId} elevation={0}><CardContent><Stack direction="row" justifyContent="space-between" gap={1}><Typography variant="h3">{person.fullName}</Typography></Stack><Typography color="text.secondary">{person.email}{person.phoneNumber?` · ${person.phoneNumber}`:''}</Typography><Stack direction="row" gap={.5} flexWrap="wrap" mt={1}>{getPersonBadges(person).map(badge=><Chip key={badge.label} size="small" label={badge.label} color={badge.color}/>)}{person.roles.map(role=><Chip key={role} size="small" label={roleLabels[role]??role}/>)}</Stack>{person.unitLinks.map(link=><Typography key={link.unitMembershipId} color="text.secondary" fontSize=".8rem" mt={1}>{link.block?`Bloco ${link.block} · `:''}{link.unitIdentifier} · {relationshipLabels[link.relationshipType]}</Typography>)}<Typography color="text.secondary" fontSize=".78rem" mt={1}>Entrada: {formatDateTime(person.joinedAt)}</Typography><Stack direction={{xs:'column',sm:'row'}} gap={1} mt={2}><Button size="small" variant="outlined" startIcon={<EditRoundedIcon/>} onClick={()=>beginEdit(person)}>Editar</Button><Button size="small" variant="outlined" startIcon={<LockResetRoundedIcon/>} disabled={!person.userActive} onClick={()=>setResetTarget(person)}>Redefinir senha temporária</Button></Stack></CardContent></Card>)}</Box>}
 <Dialog open={open} onClose={()=>!saving&&setOpen(false)} fullWidth maxWidth="sm"><Box component="form" onSubmit={e=>void submit(e)}><DialogTitle>{editing?'Editar pessoa':'Adicionar pessoa'}</DialogTitle><DialogContent><Stack gap={2} pt={1}>{error&&<Alert severity="error">{error}</Alert>}{editing&&email.trim().toLowerCase()!==editing.email.toLowerCase()&&<Alert severity="warning">Alterar o e-mail também altera a credencial usada para entrar no Comvy.</Alert>}<TextField required label="Nome completo" value={fullName} onChange={e=>setFullName(e.target.value)} slotProps={{htmlInput:{maxLength:200}}}/><TextField required type="email" label="E-mail" value={email} onChange={e=>setEmail(e.target.value)} slotProps={{htmlInput:{maxLength:254}}}/><TextField label="Telefone / WhatsApp" value={phone} onChange={e=>setPhone(e.target.value)} slotProps={{htmlInput:{maxLength:30}}}/>{editing&&<><TextField label="CPF" value={cpf} onChange={e=>setCpf(e.target.value)}/><TextField label="CNPJ" value={cnpj} onChange={e=>setCnpj(e.target.value)}/><TextField label="Endereço" value={address} onChange={e=>setAddress(e.target.value)} slotProps={{htmlInput:{maxLength:300}}}/><Stack direction={{xs:'column',sm:'row'}} gap={2}><TextField fullWidth label="Cidade" value={city} onChange={e=>setCity(e.target.value)} slotProps={{htmlInput:{maxLength:100}}}/><TextField label="UF" value={state} onChange={e=>setState(e.target.value.toUpperCase())} slotProps={{htmlInput:{maxLength:2}}} sx={{width:{xs:'100%',sm:120}}}/></Stack><FormControlLabel control={<Checkbox checked={membershipActive} onChange={e=>setMembershipActive(e.target.checked)}/>} label="Pessoa ativa neste condomínio"/></>}<TextField select label="Associar a uma unidade (opcional)" value={unitId} onChange={e=>{setUnitId(e.target.value);if(!e.target.value){setResident(false);setPrimary(false)}}}><MenuItem value="">Nenhuma unidade</MenuItem>{units.map(unit=><MenuItem key={unit.id} value={unit.id}>{unit.block?`Bloco ${unit.block} · `:''}{unit.identifier}</MenuItem>)}</TextField>{unitId&&<><TextField select label="Tipo de vínculo" value={type} onChange={e=>setType(e.target.value as RelationshipType)}>{Object.entries(relationshipLabels).map(([v,l])=><MenuItem key={v} value={v}>{l}</MenuItem>)}</TextField><FormControlLabel control={<Checkbox checked={resident} onChange={e=>{setResident(e.target.checked);if(!e.target.checked)setPrimary(false)}}/>} label="Reside na unidade"/><FormControlLabel control={<Checkbox checked={primary} onChange={e=>{setPrimary(e.target.checked);if(e.target.checked)setResident(true)}}/>} label="Residência principal"/></>}</Stack></DialogContent><DialogActions><Button onClick={()=>setOpen(false)} disabled={saving}>Cancelar</Button><Button type="submit" variant="contained" disabled={saving||!fullName.trim()||!email.trim()}>{saving?'Salvando...':editing?'Salvar alterações':'Criar conta'}</Button></DialogActions></Box></Dialog>
 <Dialog open={Boolean(resetTarget)} onClose={()=>!resetting&&setResetTarget(null)}><DialogTitle>Redefinir senha temporária?</DialogTitle><DialogContent><Typography>Uma nova senha será gerada para {resetTarget?.fullName}. A senha anterior deixará de funcionar imediatamente.</Typography></DialogContent><DialogActions><Button onClick={()=>setResetTarget(null)} disabled={resetting}>Cancelar</Button><Button variant="contained" onClick={()=>void resetPassword()} disabled={resetting}>{resetting?'Gerando...':'Gerar nova senha'}</Button></DialogActions></Dialog>
 <Dialog open={Boolean(result)} onClose={closeResult} fullWidth maxWidth="sm"><DialogTitle>{result?.reset?'Senha temporária regenerada':'Conta criada com sucesso'}</DialogTitle><DialogContent>{result?.reset&&<Alert severity="success" sx={{mb:2}}>Senha temporária regenerada.</Alert>}<Typography>Compartilhe estas credenciais de forma segura. A senha é exibida somente agora.</Typography>{result&&<Card variant="outlined" sx={{mt:2}}><CardContent><Typography fontWeight={800}>{result.fullName}</Typography><Typography>E-mail: {result.email}</Typography><Typography sx={{fontFamily:'monospace',mt:1}}>Senha temporária: {result.temporaryPassword}</Typography></CardContent></Card>}{copyFeedback&&<Alert severity={copyFeedback.error?'error':'success'} sx={{mt:2}}>{copyFeedback.message}</Alert>}</DialogContent><DialogActions>{result&&<><Button startIcon={<ContentCopyRoundedIcon/>} onClick={()=>void copy(result.temporaryPassword,'Senha copiada.')}>Copiar senha</Button><Button startIcon={<ContentCopyRoundedIcon/>} onClick={()=>void copy(temporaryCredentialsWhatsAppText(result),'Mensagem copiada.')}>Copiar mensagem para WhatsApp</Button></>}<Button variant="contained" onClick={closeResult}>Concluir</Button></DialogActions></Dialog></PageContainer>}
