import { useEffect, useState, type FormEvent } from 'react'
import {
  Alert, Box, Button, CircularProgress, Dialog, DialogActions, DialogContent,
  DialogTitle, MenuItem, Stack, TextField,
} from '@mui/material'
import { brazilianStates, formatCnpj } from '../registration'
import type { ManagementCompany, ManagementCompanyInput } from './types'
import { normalizeCnpj, normalizeOptional, validateManagementCompany } from './validation'

interface Props {
  open: boolean
  company?: ManagementCompany | null
  isSaving: boolean
  error: string
  onClose: () => void
  onSubmit: (input: ManagementCompanyInput) => Promise<void>
}

export function ManagementCompanyFormDialog({
  open, company, isSaving, error, onClose, onSubmit,
}: Props) {
  const [values, setValues] = useState({
    name: '', cnpj: '', address: '', city: '', state: '', email: '', phoneNumber: '',
  })
  const [validationError, setValidationError] = useState('')

  useEffect(() => {
    if (!open) return
    setValues({
      name: company?.name ?? '', cnpj: company?.cnpj ? formatCnpj(company.cnpj) : '',
      address: company?.address ?? '', city: company?.city ?? '',
      state: company?.state ?? '', email: company?.email ?? '',
      phoneNumber: company?.phoneNumber ?? '',
    })
    setValidationError('')
  }, [company, open])

  const set = (field: keyof typeof values, value: string) =>
    setValues(current => ({ ...current, [field]: value }))

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    if (isSaving) return
    const input: ManagementCompanyInput = {
      name: values.name.trim(), cnpj: normalizeCnpj(values.cnpj),
      address: values.address.trim(), city: values.city.trim(), state: values.state,
      email: normalizeOptional(values.email), phoneNumber: normalizeOptional(values.phoneNumber),
    }
    const message = validateManagementCompany(input)
    if (message) { setValidationError(message); return }
    await onSubmit(input)
  }

  return (
    <Dialog open={open} onClose={() => undefined} disableEscapeKeyDown fullWidth maxWidth="sm">
      <Box component="form" onSubmit={(event) => void submit(event)}>
        <DialogTitle>{company ? 'Editar administradora' : 'Nova administradora'}</DialogTitle>
        <DialogContent>
          <Stack gap={2} pt={1}>
            {(validationError || error) && <Alert severity="error">{validationError || error}</Alert>}
            <TextField autoFocus required label="Nome" value={values.name}
              onChange={e => set('name', e.target.value)} slotProps={{ htmlInput: { maxLength: 150 } }} />
            <TextField required label="CNPJ" value={values.cnpj}
              onChange={e => set('cnpj', e.target.value)} slotProps={{ htmlInput: { maxLength: 18 } }} />
            <TextField required label="Endereço" value={values.address}
              onChange={e => set('address', e.target.value)} slotProps={{ htmlInput: { maxLength: 200 } }} />
            <TextField required label="Cidade" value={values.city}
              onChange={e => set('city', e.target.value)} slotProps={{ htmlInput: { maxLength: 100 } }} />
            <TextField select required label="Estado" value={values.state}
              onChange={e => set('state', e.target.value)}>
              {brazilianStates.map(state => <MenuItem key={state} value={state}>{state}</MenuItem>)}
            </TextField>
            <TextField type="email" label="E-mail" value={values.email}
              onChange={e => set('email', e.target.value)} slotProps={{ htmlInput: { maxLength: 254 } }} />
            <TextField label="Telefone" value={values.phoneNumber}
              onChange={e => set('phoneNumber', e.target.value)} slotProps={{ htmlInput: { maxLength: 30 } }} />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={onClose} disabled={isSaving}>Cancelar</Button>
          <Button type="submit" variant="contained" disabled={isSaving}>
            {isSaving ? <CircularProgress size={20} color="inherit" /> : 'Salvar'}
          </Button>
        </DialogActions>
      </Box>
    </Dialog>
  )
}
