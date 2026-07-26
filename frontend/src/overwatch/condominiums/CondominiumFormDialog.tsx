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
  MenuItem,
  Stack,
  TextField,
  Typography,
} from '@mui/material'
import type {
  CondominiumInput,
  ManagementCompanyOption,
  OverwatchCondominium,
} from './types'
import { normalizeOptional, validateCondominium } from './validation'

interface SaveRequest {
  input: CondominiumInput
  managementCompanyId: string | null
}

interface Props {
  open: boolean
  condominium?: OverwatchCondominium | null
  managementCompanies: ManagementCompanyOption[]
  isSaving: boolean
  error: string
  onClose: () => void
  onSubmit: (request: SaveRequest) => Promise<void>
}

export function CondominiumFormDialog({
  open,
  condominium,
  managementCompanies,
  isSaving,
  error,
  onClose,
  onSubmit,
}: Props) {
  const [name, setName] = useState('')
  const [email, setEmail] = useState('')
  const [phoneNumber, setPhoneNumber] = useState('')
  const [managementCompanyId, setManagementCompanyId] = useState('')
  const [validationError, setValidationError] = useState('')
  const [pendingRequest, setPendingRequest] = useState<SaveRequest | null>(null)

  useEffect(() => {
    if (!open) return
    setName(condominium?.name ?? '')
    setEmail(condominium?.email ?? '')
    setPhoneNumber(condominium?.phoneNumber ?? '')
    setManagementCompanyId(condominium?.managementCompanyId ?? '')
    setValidationError('')
    setPendingRequest(null)
  }, [condominium, open])

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    if (isSaving) return

    const request: SaveRequest = {
      input: {
        name: name.trim(),
        email: normalizeOptional(email),
        phoneNumber: normalizeOptional(phoneNumber),
      },
      managementCompanyId: managementCompanyId || null,
    }
    const message = validateCondominium(request.input)
    if (message) {
      setValidationError(message)
      return
    }

    if (
      condominium
      && request.managementCompanyId !== condominium.managementCompanyId
    ) {
      setPendingRequest(request)
      return
    }

    await onSubmit(request)
  }

  const selectedCompany = managementCompanies.find(
    (company) => company.id === pendingRequest?.managementCompanyId,
  )

  return (
    <>
      <Dialog
        open={open}
        onClose={() => undefined}
        disableEscapeKeyDown
        fullWidth
        maxWidth="sm"
      >
        <Box component="form" onSubmit={(event) => void submit(event)}>
          <DialogTitle>{condominium ? 'Editar condomínio' : 'Novo condomínio'}</DialogTitle>
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
                slotProps={{ htmlInput: { maxLength: 200 } }}
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
              <TextField
                select
                label="Administradora"
                value={managementCompanyId}
                onChange={(event) => setManagementCompanyId(event.target.value)}
              >
                <MenuItem value="">Sem administradora</MenuItem>
                {managementCompanies
                  .filter((company) =>
                    company.isActive || company.id === condominium?.managementCompanyId)
                  .map((company) => (
                    <MenuItem key={company.id} value={company.id}>
                      {company.name}{company.isActive ? '' : ' (inativa)'}
                    </MenuItem>
                  ))}
              </TextField>
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

      <Dialog
        open={Boolean(pendingRequest)}
        onClose={() => !isSaving && setPendingRequest(null)}
        fullWidth
        maxWidth="xs"
      >
        <DialogTitle>
          {pendingRequest?.managementCompanyId
            ? 'Alterar administradora'
            : 'Remover administradora'}
        </DialogTitle>
        <DialogContent>
          {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
          <Typography>
            O condomínio continuará existindo. Apenas o vínculo administrativo
            será {pendingRequest?.managementCompanyId
              ? `alterado para ${selectedCompany?.name ?? 'a administradora selecionada'}`
              : 'removido'}.
          </Typography>
          <Alert severity="warning" sx={{ mt: 2 }}>
            Funcionários da administradora podem perder acesso futuro às
            solicitações deste condomínio quando esse módulo estiver disponível.
          </Alert>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setPendingRequest(null)} disabled={isSaving}>
            Cancelar
          </Button>
          <Button
            variant="contained"
            disabled={isSaving}
            onClick={() => {
              if (pendingRequest) void onSubmit(pendingRequest)
            }}
          >
            {isSaving ? <CircularProgress size={20} color="inherit" /> : 'Confirmar'}
          </Button>
        </DialogActions>
      </Dialog>
    </>
  )
}
