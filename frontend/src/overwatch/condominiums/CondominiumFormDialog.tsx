import { useEffect, useState, type FormEvent } from 'react'
import {
  Alert, Box, Button, Checkbox, CircularProgress, Dialog, DialogActions,
  DialogContent, DialogTitle, FormControlLabel, FormHelperText, MenuItem, Stack,
  Switch, TextField, Typography,
} from '@mui/material'
import { brazilianStates, formatCnpj } from '../registration'
import type { CondominiumInput, ManagementCompanyOption, OverwatchCondominium } from './types'
import { normalizeCnpj, normalizeOptional, validateCondominium } from './validation'

interface SaveRequest { input: CondominiumInput; managementCompanyId: string | null }
interface Props {
  open: boolean; condominium?: OverwatchCondominium | null
  managementCompanies: ManagementCompanyOption[]; isSaving: boolean; error: string
  onClose: () => void; onSubmit: (request: SaveRequest) => Promise<void>
}

export function CondominiumFormDialog({
  open, condominium, managementCompanies, isSaving, error, onClose, onSubmit,
}: Props) {
  const [values, setValues] = useState({
    name: '', email: '', cnpj: '', address: '', city: '', state: '',
    hasDoorman: false, isRemoteDoorman: false, doormanContact: '',
    whatsAppUpdatesEnabled: true,
    managementCompanyId: '',
  })
  const [validationError, setValidationError] = useState('')
  const [pendingRequest, setPendingRequest] = useState<SaveRequest | null>(null)
  const set = <K extends keyof typeof values>(field: K, value: typeof values[K]) =>
    setValues(current => ({ ...current, [field]: value }))

  useEffect(() => {
    if (!open) return
    setValues({
      name: condominium?.name ?? '', email: condominium?.email ?? '',
      cnpj: condominium?.cnpj ? formatCnpj(condominium.cnpj) : '',
      address: condominium?.address ?? '', city: condominium?.city ?? '',
      state: condominium?.state ?? '', hasDoorman: condominium?.hasDoorman ?? false,
      isRemoteDoorman: condominium?.isRemoteDoorman ?? false,
      doormanContact: condominium?.doormanContact ?? '',
      whatsAppUpdatesEnabled: condominium?.whatsAppUpdatesEnabled ?? true,
      managementCompanyId: condominium?.managementCompanyId ?? '',
    })
    setValidationError(''); setPendingRequest(null)
  }, [condominium, open])

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    const request: SaveRequest = {
      input: {
        name: values.name.trim(), email: normalizeOptional(values.email),
        cnpj: normalizeCnpj(values.cnpj), address: values.address.trim(),
        city: values.city.trim(), state: values.state,
        hasDoorman: values.hasDoorman,
        isRemoteDoorman: values.hasDoorman && values.isRemoteDoorman,
        doormanContact: values.hasDoorman ? normalizeOptional(values.doormanContact) : null,
        whatsAppUpdatesEnabled: values.whatsAppUpdatesEnabled,
      },
      managementCompanyId: values.managementCompanyId || null,
    }
    const message = validateCondominium(request.input)
    if (message) { setValidationError(message); return }
    if (condominium && request.managementCompanyId !== condominium.managementCompanyId) {
      setPendingRequest(request); return
    }
    await onSubmit(request)
  }

  return <>
    <Dialog open={open} onClose={() => undefined} disableEscapeKeyDown fullWidth maxWidth="sm">
      <Box component="form" onSubmit={(event) => void submit(event)}>
        <DialogTitle>{condominium ? 'Editar condomínio' : 'Novo condomínio'}</DialogTitle>
        <DialogContent><Stack gap={2} pt={1}>
          {(validationError || error) && <Alert severity="error">{validationError || error}</Alert>}
          <TextField autoFocus required label="Nome" value={values.name}
            onChange={e => set('name', e.target.value)} slotProps={{ htmlInput: { maxLength: 200 } }} />
          <TextField type="email" label="E-mail" value={values.email}
            onChange={e => set('email', e.target.value)} slotProps={{ htmlInput: { maxLength: 254 } }} />
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
          <FormControlLabel control={<Checkbox checked={values.hasDoorman}
            onChange={e => setValues(current => ({ ...current, hasDoorman: e.target.checked,
              isRemoteDoorman: e.target.checked && current.isRemoteDoorman,
              doormanContact: e.target.checked ? current.doormanContact : '' }))} />}
            label="Possui portaria" />
          {values.hasDoorman && <>
            <FormControlLabel control={<Checkbox checked={values.isRemoteDoorman}
              onChange={e => set('isRemoteDoorman', e.target.checked)} />} label="Portaria remota" />
            <TextField label="Contato da portaria" value={values.doormanContact}
              onChange={e => set('doormanContact', e.target.value)}
              slotProps={{ htmlInput: { maxLength: 100 } }} />
          </>}
          <Box>
            <FormControlLabel
              control={<Switch checked={values.whatsAppUpdatesEnabled}
                onChange={e => set('whatsAppUpdatesEnabled', e.target.checked)} />}
              label="Atualizações pelo WhatsApp"
            />
            <FormHelperText sx={{ ml: 1.75 }}>
              Permite enviar aos moradores atualizações de status e solicitações de resposta pelo WhatsApp.
            </FormHelperText>
          </Box>
          <TextField select label="Administradora" value={values.managementCompanyId}
            onChange={e => set('managementCompanyId', e.target.value)}>
            <MenuItem value="">Sem administradora</MenuItem>
            {managementCompanies.filter(company => company.isActive
              || company.id === condominium?.managementCompanyId)
              .map(company => <MenuItem key={company.id} value={company.id}>
                {company.name}{company.isActive ? '' : ' (inativa)'}
              </MenuItem>)}
          </TextField>
        </Stack></DialogContent>
        <DialogActions><Button onClick={onClose} disabled={isSaving}>Cancelar</Button>
          <Button type="submit" variant="contained" disabled={isSaving}>
            {isSaving ? <CircularProgress size={20} color="inherit" /> : 'Salvar'}
          </Button></DialogActions>
      </Box>
    </Dialog>
    <Dialog open={Boolean(pendingRequest)} onClose={() => !isSaving && setPendingRequest(null)}
      fullWidth maxWidth="xs">
      <DialogTitle>Alterar administradora</DialogTitle>
      <DialogContent><Typography>O vínculo administrativo será atualizado.</Typography></DialogContent>
      <DialogActions><Button onClick={() => setPendingRequest(null)}>Cancelar</Button>
        <Button variant="contained" onClick={() => pendingRequest && void onSubmit(pendingRequest)}>
          Confirmar
        </Button></DialogActions>
    </Dialog>
  </>
}
