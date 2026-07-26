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
import type { ManagerInput } from './types'
import { validateManager } from './validation'

interface Props {
  open: boolean
  isSaving: boolean
  error: string
  onClose: () => void
  onSubmit: (input: ManagerInput) => Promise<void>
}

export function ManagerFormDialog({
  open, isSaving, error, onClose, onSubmit,
}: Props) {
  const [fullName, setFullName] = useState('')
  const [email, setEmail] = useState('')
  const [validationError, setValidationError] = useState('')

  useEffect(() => {
    if (open) {
      setFullName('')
      setEmail('')
      setValidationError('')
    }
  }, [open])

  const submit = (event: FormEvent) => {
    event.preventDefault()
    const input = { fullName: fullName.trim(), email: email.trim() }
    const message = validateManager(input)
    if (message) {
      setValidationError(message)
      return
    }
    void onSubmit(input)
  }

  return (
    <Dialog
      open={open}
      onClose={() => undefined}
      disableEscapeKeyDown
      fullWidth
      maxWidth="sm"
    >
      <Box component="form" onSubmit={submit}>
        <DialogTitle>Novo síndico</DialogTitle>
        <DialogContent>
          <Stack gap={2} pt={1}>
            {(validationError || error) && (
              <Alert severity="error">{validationError || error}</Alert>
            )}
            <TextField
              autoFocus required label="Nome completo" value={fullName}
              onChange={(event) => setFullName(event.target.value)}
              slotProps={{ htmlInput: { maxLength: 200 } }}
            />
            <TextField
              required type="email" label="E-mail" value={email}
              onChange={(event) => setEmail(event.target.value)}
              slotProps={{ htmlInput: { maxLength: 254 } }}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={onClose} disabled={isSaving}>Cancelar</Button>
          <Button type="submit" variant="contained" disabled={isSaving}>
            {isSaving ? <CircularProgress size={20} color="inherit" /> : 'Criar síndico'}
          </Button>
        </DialogActions>
      </Box>
    </Dialog>
  )
}
