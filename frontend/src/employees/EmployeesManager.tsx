import { useEffect, useState } from 'react'
import { Alert, Box, Button, Card, CardContent, Checkbox, Chip, Dialog, DialogActions, DialogContent, DialogTitle, FormControlLabel, MenuItem, Stack, Tab, Tabs, TextField, Typography } from '@mui/material'
import { createEmployee, listEmployees, setEmployeeStatus, updateEmployee, type Employee, type EmployeeInput } from './api'
import { listEmployeeManagementCondominiums, type EmployeeManagementCondominium } from '../administrator/employeeManagement/api'
import { PayslipDistribution } from '../employeeDocuments/PayslipDistribution'

const blank: EmployeeInput = { condominiumId: '', fullName: '', jobTitle: '', phoneNumber: '', email: '', registrationNumber: '', admissionDate: '' }
const errorMessage = (value: unknown) =>
  (value as { response?: { data?: { message?: string; title?: string } } })?.response?.data?.message
  ?? (value as { response?: { data?: { title?: string } } })?.response?.data?.title
  ?? 'Não foi possível salvar o funcionário.'

export function EmployeesManager() {
  const [tab, setTab] = useState<'employees' | 'payslips'>('employees')
  const [selected, setSelected] = useState<string[]>([])
  const [items, setItems] = useState<Employee[]>([])
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('active')
  const [condominiumId, setCondominiumId] = useState('')
  const [condominiums, setCondominiums] = useState<EmployeeManagementCondominium[]>([])
  const [editing, setEditing] = useState<Employee | null>(null)
  const [form, setForm] = useState<EmployeeInput & { isActive: boolean }>({ ...blank, isActive: true })
  const [dialog, setDialog] = useState(false)
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(true)

  const load = () => {
    setLoading(true)
    return listEmployees({ condominiumId: condominiumId || undefined, search: search || undefined, status: status || undefined })
      .then(setItems)
      .catch(() => setError('Não foi possível carregar os funcionários.'))
      .finally(() => setLoading(false))
  }
  // The request intentionally reloads when the condominium, search or status filter changes.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  useEffect(() => { void load() }, [condominiumId, search, status])
  useEffect(() => { void listEmployeeManagementCondominiums().then(setCondominiums).catch(() => setError('Não foi possível carregar os condomínios.')) }, [])

  const open = (item?: Employee) => {
    setEditing(item ?? null)
    setDialog(true)
    setError('')
    setForm(item
      ? {
        condominiumId: item.condominiumId, fullName: item.fullName, cpf: item.cpf ?? '', jobTitle: item.jobTitle ?? '', phoneNumber: item.phoneNumber ?? '',
        email: item.email ?? '', registrationNumber: item.registrationNumber ?? '',
        admissionDate: item.admissionDate ?? '', isActive: item.isActive,
      }
      : { ...blank, condominiumId: condominiumId || condominiums[0]?.id || '', isActive: true })
  }

  const save = async () => {
    try {
      const input: EmployeeInput = {
        condominiumId: form.condominiumId, fullName: form.fullName, cpf: form.cpf, jobTitle: form.jobTitle, phoneNumber: form.phoneNumber,
        email: form.email, registrationNumber: form.registrationNumber, admissionDate: form.admissionDate,
      }
      const saved = editing ? await updateEmployee(editing.id, input) : await createEmployee(input)
      const result = editing && editing.isActive !== form.isActive
        ? await setEmployeeStatus(saved.id, form.isActive)
        : saved
      setItems(old => editing ? old.map(x => x.id === result.id ? result : x) : [result, ...old])
      setDialog(false)
    } catch (e) {
      setError(errorMessage(e))
    }
  }

  const toggleStatus = async (item: Employee) => {
    try {
      const updated = await setEmployeeStatus(item.id, !item.isActive)
      setItems(old => old.map(x => x.id === updated.id ? updated : x))
    } catch (e) {
      setError(errorMessage(e))
    }
  }

  const selectAll = () => setSelected(items.map(item => item.id))
  const selectedCondominiumIds = [...new Set(items.filter(item => selected.includes(item.id)).map(item => item.condominiumId))]
  const selectedCondominiumId = selected.length > 0 ? 'multi-condominium' : ''
  return (
    <Stack gap={2}>
      <Box display="flex" justifyContent="space-between" gap={2} flexWrap="wrap">
        <Box>
          <Typography variant="h1">Funcionários</Typography>
          <Typography color="text.secondary">Funcionários deste condomínio.</Typography>
        </Box>
        {tab === 'employees' && <Button variant="contained" onClick={() => open()}>Novo funcionário</Button>}
      </Box>
      <Tabs value={tab} onChange={(_, value: 'employees' | 'payslips') => setTab(value)}>
        <Tab value="employees" label="Funcionários" />
        <Tab value="payslips" label="Holerites" />
      </Tabs>
      {tab === 'payslips' ? (selectedCondominiumId
        ? <PayslipDistribution selectedEmployeeIds={selected} />
        : <Alert severity="info">Selecione funcionários de um único condomínio para distribuir holerites.</Alert>) : <>
      {error && <Alert severity="error">{error}</Alert>}
      <Stack direction={{ xs: 'column', sm: 'row' }} gap={1.5}>
        <TextField label="Buscar" value={search} onChange={e => setSearch(e.target.value)} fullWidth />
        <TextField select label="Status" value={status} onChange={e => setStatus(e.target.value)} sx={{ minWidth: 160 }}>
          <MenuItem value="active">Ativos</MenuItem>
          <MenuItem value="inactive">Inativos</MenuItem>
          <MenuItem value="">Todos</MenuItem>
        </TextField>
        <TextField select label="Condomínio" value={condominiumId} onChange={e => setCondominiumId(e.target.value)} sx={{ minWidth: 220 }}>
          <MenuItem value="">Todos</MenuItem>
          {condominiums.map(c => <MenuItem value={c.id} key={c.id}>{c.name}</MenuItem>)}
        </TextField>
      </Stack>
      <Stack direction="row" gap={1} alignItems="center">
        <Button size="small" onClick={selectAll}>Selecionar todos filtrados</Button>
        <Typography variant="body2" color="text.secondary">{selected.length} selecionado(s)</Typography>
      </Stack>
      {loading
        ? <Alert severity="info">Carregando funcionários…</Alert>
        : items.length === 0
          ? <Alert severity="info">Nenhum funcionário encontrado.</Alert>
          : items.map(item => (
            <Card key={item.id} variant="outlined">
              <CardContent>
                <Stack direction={{ xs: 'column', sm: 'row' }} justifyContent="space-between" gap={2}>
                  <Checkbox checked={selected.includes(item.id)} onChange={event => setSelected(current => event.target.checked ? [...new Set([...current, item.id])] : current.filter(id => id !== item.id))} inputProps={{ 'aria-label': `Selecionar ${item.fullName}` }} />
                  <Box>
                    <Typography fontWeight={800}>{item.fullName}</Typography>
                    {item.jobTitle && <Typography color="text.secondary">{item.jobTitle}</Typography>}
                    <Stack direction="row" flexWrap="wrap" gap={.75} mt={1}>
                      <Chip size="small" label={condominiums.find(c => c.id === item.condominiumId)?.name ?? 'Condomínio'} />
                      {item.cpf && <Chip size="small" label={`CPF ${item.cpf}`} />}
                      {item.registrationNumber && <Chip size="small" label={`Matrícula ${item.registrationNumber}`} />}
                      {item.phoneNumber && <Chip size="small" label={item.phoneNumber} />}
                      {item.email && <Chip size="small" label={item.email} />}
                      <Chip size="small" color={item.isActive ? 'success' : 'default'} label={item.isActive ? 'Ativo' : 'Inativo'} />
                    </Stack>
                  </Box>
                  <Stack direction="row" gap={1} alignSelf={{ xs: 'flex-start', sm: 'center' }}>
                    <Button onClick={() => open(item)}>Editar</Button>
                    <Button onClick={() => void toggleStatus(item)}>{item.isActive ? 'Inativar' : 'Ativar'}</Button>
                  </Stack>
                </Stack>
              </CardContent>
            </Card>
          ))}
      <Dialog open={dialog} onClose={() => setDialog(false)} fullWidth maxWidth="sm">
        <DialogTitle>{editing ? 'Editar funcionário' : 'Novo funcionário'}</DialogTitle>
        <DialogContent dividers>
          <Stack gap={2}>
            <TextField label="Nome completo" value={form.fullName} onChange={e => setForm({ ...form, fullName: e.target.value })} required fullWidth />
            <TextField select label="Condomínio" value={form.condominiumId} onChange={e => setForm({ ...form, condominiumId: e.target.value })} required fullWidth>
              {condominiums.map(c => <MenuItem value={c.id} key={c.id}>{c.name}</MenuItem>)}
            </TextField>
            <TextField label="CPF" value={form.cpf ?? ''} onChange={e => setForm({ ...form, cpf: e.target.value })} fullWidth />
            <TextField label="Função / cargo" value={form.jobTitle} onChange={e => setForm({ ...form, jobTitle: e.target.value })} fullWidth />
            <Stack direction={{ xs: 'column', sm: 'row' }} gap={1}>
              <TextField label="Telefone" value={form.phoneNumber} onChange={e => setForm({ ...form, phoneNumber: e.target.value })} fullWidth />
              <TextField label="E-mail" value={form.email} onChange={e => setForm({ ...form, email: e.target.value })} fullWidth />
            </Stack>
            <Stack direction={{ xs: 'column', sm: 'row' }} gap={1}>
              <TextField label="Matrícula" value={form.registrationNumber} onChange={e => setForm({ ...form, registrationNumber: e.target.value })} fullWidth />
              <TextField label="Data de admissão" type="date" value={form.admissionDate} onChange={e => setForm({ ...form, admissionDate: e.target.value })} slotProps={{ inputLabel: { shrink: true } }} fullWidth />
            </Stack>
            {editing && <FormControlLabel control={<Checkbox checked={form.isActive} onChange={e => setForm({ ...form, isActive: e.target.checked })} />} label="Ativo" />}
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDialog(false)}>Cancelar</Button>
          <Button variant="contained" onClick={() => void save()}>Salvar</Button>
        </DialogActions>
      </Dialog>
      </>}
    </Stack>
  )
}
