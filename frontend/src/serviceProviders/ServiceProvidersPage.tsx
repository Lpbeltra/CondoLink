import { KeyboardEvent, useCallback, useEffect, useState } from 'react'
import { Alert, Autocomplete, Box, Button, Checkbox, Chip, Divider, Drawer, IconButton, Menu, MenuItem, Stack, TextField, Typography } from '@mui/material'
import MoreVertRoundedIcon from '@mui/icons-material/MoreVertRounded'
import CloseRoundedIcon from '@mui/icons-material/CloseRounded'
import EditRoundedIcon from '@mui/icons-material/EditRounded'
import ContentCopyRoundedIcon from '@mui/icons-material/ContentCopyRounded'
import { Dialog, DialogActions, DialogContent, DialogTitle, FormControlLabel } from '@mui/material'
import { PageContainer } from '../components/PageContainer'
import { PageHeader } from '../components/PageHeader'
import { useGuardedLoad } from '../components/useGuardedLoad'
import { useManagementContext } from '../management/ManagementContext'
import { hasWhatsAppPhone, OpenProviderWhatsAppButton } from '../requests/components/ProviderWhatsAppDialog'
import { createServiceProvider, listServiceProviders, updateServiceProvider, type PixKeyType, type ServiceProvider, type ServiceProviderInput } from './api'

const blank: ServiceProviderInput = { name:'', companyName:'', specialties:[], phone:'', email:'', pixKey:'', pixKeyType:null, notes:'', isMine:true, condominiumIds:[], isActive:true }
const loadErrorMessage = () => 'Não foi possível carregar prestadores.'
const saveErrorMessage=(value:unknown)=>(value as {response?:{data?:{error?:string;title?:string}}})?.response?.data?.error??(value as {response?:{data?:{title?:string}}})?.response?.data?.title??'Não foi possível salvar o prestador.'

function formatPhone(phone:string) {
  const digits=phone.replace(/\D/g,'')
  if(digits.length===13&&digits.startsWith('55')) return `+55 (${digits.slice(2,4)}) ${digits.slice(4,9)}-${digits.slice(9)}`
  if(digits.length===12&&digits.startsWith('55')) return `+55 (${digits.slice(2,4)}) ${digits.slice(4,8)}-${digits.slice(8)}`
  if(digits.length===11) return `(${digits.slice(0,2)}) ${digits.slice(2,7)}-${digits.slice(7)}`
  if(digits.length===10) return `(${digits.slice(0,2)}) ${digits.slice(2,6)}-${digits.slice(6)}`
  return phone
}

function specialtySummary(item:ServiceProvider) {
  const values=item.specialties?.length?item.specialties:[item.specialty]
  return values.length>1?`${values.slice(0,2).join(' · ')}${values.length>2?` +${values.length-2}`:''}`:values[0]
}

function linkSummary(item:ServiceProvider) {
  const names=item.condominiums.map(x=>x.name)
  const scope=names.length>2?`${names.slice(0,2).join(' · ')} +${names.length-2}`:names.join(' · ')
  return [item.isMine?'Meu prestador':'',scope].filter(Boolean).join(' · ')||'Sem vínculo condominial'
}

export function ServiceProvidersPage() {
  const { condominiums, activeCondominiumId }=useManagementContext()
  const [search,setSearch]=useState(''),[debouncedSearch,setDebouncedSearch]=useState(''),[status,setStatus]=useState('active'),[availability,setAvailability]=useState(''),[editing,setEditing]=useState<ServiceProvider|null>(null),[selected,setSelected]=useState<ServiceProvider|null>(null),[form,setForm]=useState(blank),[dialog,setDialog]=useState(false),[menu,setMenu]=useState<{anchor:HTMLElement;item:ServiceProvider}|null>(null)
  const fetchProviders=useCallback(()=>listServiceProviders({search:debouncedSearch||undefined,status,availability:availability||undefined,...(activeCondominiumId?{condominiumId:activeCondominiumId}:{})}),[activeCondominiumId,availability,debouncedSearch,status])
  const { data, isLoading, error, reload, setData, setError }=useGuardedLoad(fetchProviders,loadErrorMessage)
  const items=data??[]

  useEffect(()=>{const timer=window.setTimeout(()=>setDebouncedSearch(search),250);return()=>window.clearTimeout(timer)},[search])
  useEffect(()=>{setData(null);setSelected(null)},[activeCondominiumId,availability,debouncedSearch,setData,status])

  const openEditor=(item?:ServiceProvider)=>{setEditing(item??null);setDialog(true);setError('');setForm(item?{name:item.name,companyName:item.companyName??'',specialties:item.specialties?.length?item.specialties:[item.specialty],phone:item.phone,email:item.email??'',pixKey:item.pixKey??'',pixKeyType:item.pixKeyType,notes:item.notes??'',isMine:item.isMine,condominiumIds:item.condominiums.map(x=>x.condominiumId),isActive:item.isActive}:{...blank,condominiumIds:activeCondominiumId?[activeCondominiumId]:[]})}
  const save=async()=>{try{if(editing) await updateServiceProvider(editing.id,form,activeCondominiumId??undefined);else await createServiceProvider(form);await reload();setDialog(false)}catch(e){setError(saveErrorMessage(e))}}
  const specialties=(values:string[])=>setForm({...form,specialties:Array.from(new Map(values.map(x=>[x.trim().toLowerCase(),x.trim()])).values()).filter(Boolean)})
  const onRowKeyDown=(event:KeyboardEvent<HTMLDivElement>,item:ServiceProvider)=>{if(event.key==='Enter'||event.key===' '){event.preventDefault();setSelected(item)}}
  const emptyMessage=search.trim()?'Nenhum prestador encontrado para esta busca.':status!=='active'||availability?'Nenhum prestador corresponde aos filtros.':'Nenhum prestador cadastrado neste contexto.'

  return <PageContainer maxWidth={1200}>
    <PageHeader title="Prestadores" description="Encontre rapidamente quem pode ajudar no condomínio." actions={<Button variant="contained" onClick={()=>openEditor()}>Novo prestador</Button>} />
    <Stack gap={{xs:2,md:2.5}}>
      <Stack direction={{xs:'column',sm:'row'}} gap={1.25} alignItems={{sm:'center'}}>
        <TextField label="Buscar" placeholder="Nome, empresa, especialidade ou telefone" value={search} onChange={e=>setSearch(e.target.value)} fullWidth size="small" />
        <TextField select label="Status" value={status} onChange={e=>setStatus(e.target.value)} size="small" sx={{minWidth:{sm:150}}}><MenuItem value="active">Ativos</MenuItem><MenuItem value="inactive">Inativos</MenuItem><MenuItem value="">Todos</MenuItem></TextField>
        <TextField select label="Vínculo" value={availability} onChange={e=>setAvailability(e.target.value)} size="small" sx={{minWidth:{sm:180}}}><MenuItem value="">Todos</MenuItem><MenuItem value="mine">Meus prestadores</MenuItem><MenuItem value="condominium">Do condomínio</MenuItem></TextField>
      </Stack>
      {error&&<Alert severity="error">{error}</Alert>}
      {isLoading?<Alert severity="info">Carregando prestadores…</Alert>:data&&items.length===0?<Alert severity="info" action={!search.trim()&&status==='active'&&availability===''?<Button color="inherit" size="small" onClick={()=>openEditor()}>Novo prestador</Button>:undefined}>{emptyMessage}</Alert>:<Box role="list" sx={{borderTop:'1px solid',borderColor:'divider'}}>
        <Box sx={{display:{xs:'none',md:'grid'},gridTemplateColumns:'minmax(240px,1.5fr) minmax(150px,1fr) minmax(190px,1.2fr) minmax(150px,1fr) 112px',gap:2,px:2,py:1,color:'text.secondary'}}><Typography variant="overline">Prestador</Typography><Typography variant="overline">Especialidade</Typography><Typography variant="overline">Vínculo</Typography><Typography variant="overline">Contato</Typography><Typography variant="overline" textAlign="right">Ações</Typography></Box>
        {items.map(item=><Box key={item.id} role="listitem" tabIndex={0} aria-label={`Ver detalhes de ${item.name}`} onClick={()=>setSelected(item)} onKeyDown={event=>onRowKeyDown(event,item)} sx={{display:'grid',gridTemplateColumns:{xs:'1fr auto',md:'minmax(240px,1.5fr) minmax(150px,1fr) minmax(190px,1.2fr) minmax(150px,1fr) 112px'},gap:{xs:1,md:2},alignItems:'center',px:{xs:1,md:2},py:{xs:1.5,md:1.75},borderBottom:'1px solid',borderColor:'divider',cursor:'pointer','&:hover':{bgcolor:'action.hover'},'&:focus-visible':{outline:'2px solid',outlineColor:'primary.main',outlineOffset:-2}}}>
          <Box minWidth={0}><Typography fontWeight={750} noWrap>{item.name}</Typography>{item.companyName&&<Typography variant="body2" color="text.secondary" noWrap>{item.companyName}</Typography>}<Typography sx={{display:{xs:'block',md:'none'},mt:.5}} variant="body2" color="text.secondary">{specialtySummary(item)}</Typography><Typography sx={{display:{xs:'block',md:'none'}}} variant="body2" color="text.secondary" noWrap>{linkSummary(item)}</Typography></Box>
          <Typography sx={{display:{xs:'none',md:'block'}}} color="text.secondary" noWrap>{specialtySummary(item)}</Typography>
          <Typography sx={{display:{xs:'none',md:'block'}}} color="text.secondary" noWrap>{linkSummary(item)}</Typography>
          <Box sx={{display:{xs:'none',md:'block'},minWidth:0}}><Typography color="text.secondary" noWrap>{formatPhone(item.phone)}</Typography>{!item.isActive&&<Typography variant="caption" color="text.secondary">Inativo</Typography>}</Box>
          <Stack direction="row" gap={.5} justifyContent="flex-end" onClick={event=>event.stopPropagation()}><>{hasWhatsAppPhone(item.phone)&&<OpenProviderWhatsAppButton phone={item.phone} />}</><IconButton aria-label={`Mais ações de ${item.name}`} onClick={event=>setMenu({anchor:event.currentTarget,item})}><MoreVertRoundedIcon /></IconButton></Stack>
          <Box sx={{display:{xs:'block',md:'none'},gridColumn:'1 / -1'}}><Typography variant="body2" color="text.secondary">{formatPhone(item.phone)}{!item.isActive&&' · Inativo'}</Typography></Box>
        </Box>)}
      </Box>}
    </Stack>

    <Menu anchorEl={menu?.anchor} open={Boolean(menu)} onClose={()=>setMenu(null)}><MenuItem onClick={()=>{if(menu){openEditor(menu.item);setMenu(null)}}}><EditRoundedIcon fontSize="small" sx={{mr:1}}/>Editar</MenuItem></Menu>

    <Drawer anchor="right" open={Boolean(selected)} onClose={()=>setSelected(null)} ModalProps={{'aria-labelledby':'service-provider-detail-title'}} PaperProps={{sx:{width:{xs:'100%',sm:420},maxWidth:'100%',p:{xs:2,sm:3}}}}>
      {selected&&<Stack gap={2.5} height="100%">
        <Stack direction="row" justifyContent="space-between" gap={2} alignItems="flex-start"><Box minWidth={0}><Typography id="service-provider-detail-title" variant="h2" sx={{wordBreak:'break-word'}}>{selected.name}</Typography>{selected.companyName&&<Typography color="text.secondary">{selected.companyName}</Typography>}{!selected.isActive&&<Typography variant="body2" color="text.secondary" mt={.5}>Inativo</Typography>}</Box><IconButton aria-label="Fechar detalhes" onClick={()=>setSelected(null)}><CloseRoundedIcon/></IconButton></Stack>
        <Stack direction="row" gap={1} flexWrap="wrap">{hasWhatsAppPhone(selected.phone)&&<OpenProviderWhatsAppButton phone={selected.phone}/>}<Button startIcon={<EditRoundedIcon/>} onClick={()=>{openEditor(selected);setSelected(null)}}>Editar</Button></Stack>
        <Divider/>
        <Box><Typography variant="overline" color="text.secondary">Contato</Typography><Typography mt={.5}>{formatPhone(selected.phone)}</Typography>{selected.email&&<Typography color="text.secondary">{selected.email}</Typography>}</Box>
        <Box><Typography variant="overline" color="text.secondary">Especialidades</Typography><Stack direction="row" gap={.75} flexWrap="wrap" mt={.75}>{(selected.specialties?.length?selected.specialties:[selected.specialty]).map(value=><Chip key={value} size="small" label={value}/>)}</Stack></Box>
        <Box><Typography variant="overline" color="text.secondary">Vínculos</Typography>{selected.isMine&&<Typography mt={.5}>✓ Meu prestador</Typography>}{selected.condominiums.length>0&&<><Typography mt={selected.isMine?1:.5} color="text.secondary">Condomínios</Typography>{selected.condominiums.map(condo=><Typography key={condo.condominiumId}>{condo.name}</Typography>)}</>}</Box>
        {(selected.pixKey||selected.pixKeyType)&&<Box><Typography variant="overline" color="text.secondary">PIX</Typography>{selected.pixKeyType&&<Typography mt={.5}>Tipo: {selected.pixKeyType}</Typography>}{selected.pixKey&&<Stack direction="row" alignItems="center" gap={.5}><Typography sx={{wordBreak:'break-all'}}>{selected.pixKey}</Typography><IconButton size="small" aria-label="Copiar chave PIX" onClick={()=>void navigator.clipboard?.writeText(selected.pixKey??'')}><ContentCopyRoundedIcon fontSize="small"/></IconButton></Stack>}</Box>}
        {selected.notes&&<Box><Typography variant="overline" color="text.secondary">Observações</Typography><Typography mt={.5} sx={{whiteSpace:'pre-wrap',wordBreak:'break-word'}}>{selected.notes}</Typography></Box>}
      </Stack>}
    </Drawer>

    <Dialog open={dialog} onClose={()=>setDialog(false)} fullWidth maxWidth="sm"><DialogTitle>{editing?'Editar prestador':'Novo prestador'}</DialogTitle><DialogContent dividers><Stack gap={2.25}><Box><Typography variant="overline" color="text.secondary">Prestador</Typography><Stack gap={1.5} mt={.75}><Stack direction={{xs:'column',sm:'row'}} gap={1}><TextField label="Nome" value={form.name} onChange={e=>setForm({...form,name:e.target.value})} required fullWidth/><TextField label="Empresa" value={form.companyName} onChange={e=>setForm({...form,companyName:e.target.value})} fullWidth/></Stack><Autocomplete multiple freeSolo options={[]} value={form.specialties} onChange={(_,values)=>specialties(values)} renderTags={(value,getTagProps)=>value.map((option,index)=><Chip {...getTagProps({index})} key={option.toLowerCase()} label={option}/>)} renderInput={params=><TextField {...params} label="Especialidades" placeholder="Digite e pressione Enter" required/>}/></Stack></Box><Divider/><Box><Typography variant="overline" color="text.secondary">Contato</Typography><Stack gap={1.5} mt={.75}><TextField label="Telefone / WhatsApp" value={form.phone} onChange={e=>setForm({...form,phone:e.target.value})} required/><TextField label="E-mail" value={form.email} onChange={e=>setForm({...form,email:e.target.value})}/></Stack></Box><Divider/><Box><Typography variant="overline" color="text.secondary">Vínculo</Typography><Stack mt={.75}><FormControlLabel control={<Checkbox checked={form.isMine} onChange={e=>setForm({...form,isMine:e.target.checked})}/>} label="Meu prestador — vínculo pessoal"/><FormControlLabel control={<Checkbox checked={form.condominiumIds.length>0} onChange={e=>setForm({...form,condominiumIds:e.target.checked?(activeCondominiumId?[activeCondominiumId]:[]):[]})}/>} label="Prestador do condomínio — vínculo com a gestão"/>{form.condominiumIds.length>0&&<TextField select label="Condomínios" value={form.condominiumIds} SelectProps={{multiple:true}} onChange={e=>setForm({...form,condominiumIds:typeof e.target.value==='string'?e.target.value.split(','):e.target.value as string[]})} fullWidth>{condominiums.map(c=><MenuItem key={c.id} value={c.id}>{c.name}</MenuItem>)}</TextField>}</Stack></Box><Divider/><Box><Typography variant="overline" color="text.secondary">Pagamento</Typography><Stack direction={{xs:'column',sm:'row'}} gap={1.5} mt={.75}><TextField select label="Tipo PIX" value={form.pixKeyType??''} onChange={e=>setForm({...form,pixKeyType:(e.target.value||null) as PixKeyType|null})} fullWidth><MenuItem value="">Sem PIX</MenuItem>{(['Cpf','Cnpj','Email','Phone','Random'] as PixKeyType[]).map(x=><MenuItem value={x} key={x}>{x}</MenuItem>)}</TextField>{form.pixKeyType&&<TextField label="Chave PIX" value={form.pixKey} onChange={e=>setForm({...form,pixKey:e.target.value})} fullWidth/>}</Stack></Box><Divider/><Box><Typography variant="overline" color="text.secondary">Observações</Typography><TextField label="Observações" multiline minRows={2} value={form.notes} onChange={e=>setForm({...form,notes:e.target.value})} fullWidth sx={{mt:.75}}/></Box></Stack></DialogContent><DialogActions><Button onClick={()=>setDialog(false)}>Cancelar</Button><Button variant="contained" onClick={()=>void save()}>Salvar</Button></DialogActions></Dialog>
  </PageContainer>
}
