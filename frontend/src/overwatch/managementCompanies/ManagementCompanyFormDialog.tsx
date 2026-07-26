import { useEffect, useState, type FormEvent } from 'react'
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Stack,
  TextField,
} from '@mui/material'
import type { ManagementCompany, ManagementCompanyInput } from './types'
import {
  normalizeOptional,
  validateManagementCompany,
} from './validation'

interface Props {
  open: boolean
  company?: ManagementCompany | null
  isSaving: boolean
  error: string
  onClose: () => void
  onSubmit: (input: ManagementCompanyInput) => Promise<void>
}

export function ManagementCompanyFormDialog({
  open,
  company,
  isSaving,
  error,
  onClose,
  onSubmit,
}: Props) {
  const [name, setName] = useState('')
  const [legalName, setLegalName] = useState('')
  const [document, setDocument] = useState('')
  const [email, setEmail] = useState('')
  const [phoneNumber, setPhoneNumber] = useState('')
  const [validationError, setValidationError] = useState('')

  useEffect(() => {
    if (!open) return
    setName(company?.name ?? '')
    setLegalName(company?.legalName ?? '')
    setDocument(company?.document ?? '')
    setEmail(company?.email ?? '')
    setPhoneNumber(company?.phoneNumber ?? '')
    setValidationError('')
  }, [company, open])

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    if (isSaving) return

    const input: ManagementCompanyInput = {
      name: name.trim(),
      legalName: normalizeOptional(legalName),
      document: normalizeOptional(document),
      email: normalizeOptional(email),
      phoneNumber: normalizeOptional(phoneNumber),
    }
    const message = validateManagementCompany(input)
    if (message) {
      setValidationError(message)
      return
    }
    await onSubmit(input)
  }

  return (
    <Dialog
      open={open}
      onClose={() => undefined}
      disableEscapeKeyDown
      fullWidth
      maxWidth="sm"
    >
      <Box component="form" onSubmit={(event) => void submit(event)}>
        <DialogTitle>{company ? 'Editar administradora' : 'Nova administradora'}</DialogTitle>
        <DialogContent>
          <Stack gap={2} pt={1}>
            {(validationError || error) && (
              <Alert severity="error">{validationError || error}</Alert>
            )}
            <TextField
              autoFocus
              required
              label="Nome"
              value={name}
              onChange={(event) => setName(event.target.value)}
              slotProps={{ htmlInput: { maxLength: 150 } }}
            />
            <TextField
              label="Razão social"
              value={legalName}
              onChange={(event) => setLegalName(event.target.value)}
              slotProps={{ htmlInput: { maxLength: 200 } }}
            />
            <TextField
              label="Documento"
              value={document}
              onChange={(event) => setDocument(event.target.value)}
              slotProps={{ htmlInput: { maxLength: 20 } }}
            />
            <TextField
              type="email"
              label="E-mail"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              slotProps={{ htmlInput: { maxLength: 254 } }}
            />
            <TextField
              label="Telefone"
              value={phoneNumber}
              onChange={(event) => setPhoneNumber(event.target.value)}
              slotProps={{ htmlInput: { maxLength: 30 } }}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={onClose} disabled={isSaving}>Cancelar</Button>
          <Button
            type="submit"
            variant="contained"
            disabled={isSaving || !name.trim()}
          >
            {isSaving ? <CircularProgress size={20} color="inherit" /> : 'Salvar'}
          </Button>
        </DialogActions>
      </Box>
    </Dialog>
  )
}
