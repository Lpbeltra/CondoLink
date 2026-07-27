import { useEffect, useState, type FormEvent } from 'react'
import {
  Alert, Box, Button, CircularProgress, Dialog, DialogActions, DialogContent,
  DialogTitle, MenuItem, Stack, TextField,
} from '@mui/material'
import { brazilianStates, formatCnpj, formatCpf } from '../registration'
import type { ManagerInput, OverwatchManager } from './types'
import { validateManager } from './validation'

interface Props {
  open: boolean
  manager?: OverwatchManager | null
  isSaving: boolean
  error: string
  onClose: () => void
  onSubmit: (input: ManagerInput) => Promise<void>
}

export function ManagerFormDialog({
  open, manager, isSaving, error, onClose, onSubmit,
}: Props) {
  const [values, setValues] = useState({
    fullName: '', email: '', phoneNumber: '', cpf: '', cnpj: '',
    address: '', city: '', state: '',
  })
  const [validationError, setValidationError] = useState('')
  const set = (field: keyof typeof values, value: string) =>
    setValues(current => ({ ...current, [field]: value }))

  useEffect(() => {
    if (!open) return
    setValues({
      fullName: manager?.fullName ?? '', email: manager?.email ?? '',
      phoneNumber: manager?.phoneNumber ?? '',
      cpf: manager?.cpf ? formatCpf(manager.cpf) : '',
      cnpj: manager?.cnpj ? formatCnpj(manager.cnpj) : '',
      address: manager?.address ?? '', city: manager?.city ?? '',
      state: manager?.state ?? '',
    })
    setValidationError('')
  }, [manager, open])

  const submit = (event: FormEvent) => {
    event.preventDefault()
    const input: ManagerInput = {
      fullName: values.fullName.trim(), email: values.email.trim(),
      phoneNumber: values.phoneNumber.trim() || null,
      cpf: values.cpf.trim() || null, cnpj: values.cnpj.trim() || null,
      address: values.address.trim() || null, city: values.city.trim() || null,
      state: values.state || null,
    }
    const message = validateManager(input)
    if (message) { setValidationError(message); return }
    void onSubmit(input)
  }

  return <Dialog open={open} onClose={() => undefined} disableEscapeKeyDown fullWidth maxWidth="sm">
    <Box component="form" onSubmit={submit}>
      <DialogTitle>{manager ? 'Editar síndico' : 'Novo síndico'}</DialogTitle>
      <DialogContent><Stack gap={2} pt={1}>
        {(validationError || error) && <Alert severity="error">{validationError || error}</Alert>}
        <TextField autoFocus required label="Nome completo" value={values.fullName}
          onChange={e => set('fullName', e.target.value)} slotProps={{ htmlInput: { maxLength: 200 } }} />
        <TextField required disabled={Boolean(manager)} type="email" label="E-mail"
          value={values.email} onChange={e => set('email', e.target.value)}
          slotProps={{ htmlInput: { maxLength: 254 } }} />
        <TextField label="Telefone / WhatsApp" value={values.phoneNumber}
          onChange={e => set('phoneNumber', e.target.value)} slotProps={{ htmlInput: { maxLength: 30 } }} />
        <TextField label="CPF" value={values.cpf} onChange={e => set('cpf', e.target.value)}
          slotProps={{ htmlInput: { maxLength: 14 } }} />
        <TextField label="CNPJ" value={values.cnpj} onChange={e => set('cnpj', e.target.value)}
          slotProps={{ htmlInput: { maxLength: 18 } }} />
        <TextField label="Endereço" value={values.address}
          onChange={e => set('address', e.target.value)} slotProps={{ htmlInput: { maxLength: 200 } }} />
        <TextField label="Cidade" value={values.city}
          onChange={e => set('city', e.target.value)} slotProps={{ htmlInput: { maxLength: 100 } }} />
        <TextField select label="Estado" value={values.state} onChange={e => set('state', e.target.value)}>
          <MenuItem value="">Não informado</MenuItem>
          {brazilianStates.map(state => <MenuItem key={state} value={state}>{state}</MenuItem>)}
        </TextField>
      </Stack></DialogContent>
      <DialogActions><Button onClick={onClose} disabled={isSaving}>Cancelar</Button>
        <Button type="submit" variant="contained" disabled={isSaving}>
          {isSaving ? <CircularProgress size={20} color="inherit" /> : manager ? 'Salvar' : 'Criar síndico'}
        </Button></DialogActions>
    </Box>
  </Dialog>
}
