import { useState, type FormEvent } from 'react'
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Stack,
  TextField,
  Typography,
} from '@mui/material'
import LockResetRoundedIcon from '@mui/icons-material/LockResetRounded'
import { useLocation, useNavigate } from 'react-router-dom'
import { api } from '../services/api'
import { authError } from '../auth/errors'
import { useAuth } from '../auth/AuthContext'
import { authenticatedEntryPath } from '../auth/routeAccess'
import { PasswordVisibilityAdornment } from '../components/PasswordVisibilityAdornment'
import { AuthShell } from '../layout/AuthShell'

interface ChangePasswordLocationState {
  email?: string
  temporaryPassword?: string
}

export function ChangePasswordPage() {
  const location = useLocation()
  const navigate = useNavigate()
  const { login } = useAuth()
  const state = location.state as ChangePasswordLocationState | null
  const [email, setEmail] = useState(state?.email ?? '')
  const [temporaryPassword, setTemporaryPassword] = useState(
    state?.temporaryPassword ?? '',
  )
  const [newPassword, setNewPassword] = useState('')
  const [confirmation, setConfirmation] = useState('')
  const [error, setError] = useState('')
  const [saving, setSaving] = useState(false)
  const [showTemporaryPassword, setShowTemporaryPassword] = useState(false)
  const [showNewPassword, setShowNewPassword] = useState(false)
  const [showConfirmation, setShowConfirmation] = useState(false)

  const validationError = newPassword.length > 0 && newPassword.length < 8
    ? 'A nova senha deve possuir ao menos 8 caracteres.'
    : confirmation.length > 0 && newPassword !== confirmation
      ? 'A confirmação da senha não confere.'
      : ''

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    if (saving || validationError) return
    setSaving(true)
    setError('')
    try {
      await api.post('/auth/change-temporary-password', {
        email: email.trim(),
        temporaryPassword,
        newPassword,
        confirmation,
      })
      await login(email.trim(), newPassword)
      navigate(authenticatedEntryPath, {
        replace: true,
        state: { passwordChanged: true },
      })
    } catch (requestError) {
      setError(authError(requestError))
    } finally {
      setSaving(false)
    }
  }

  return (
    <AuthShell>
      <Stack spacing={1} mb={4}>
        <Typography variant="h1">Atualize sua senha</Typography>
        <Typography color="text.secondary">
          Você precisa substituir sua senha temporária antes de continuar.
        </Typography>
      </Stack>
      <Box component="form" onSubmit={event => void submit(event)}>
        <Stack gap={2}>
                {error && <Alert severity="error">{error}</Alert>}
                  <TextField
                    required
                    type="email"
                    label="E-mail"
                    autoComplete="email"
                    value={email}
                    onChange={event => setEmail(event.target.value)}
                    disabled={saving}
                  />
                  <TextField
                    required
                    type={showTemporaryPassword ? 'text' : 'password'}
                    label="Senha temporária"
                    autoComplete="current-password"
                    value={temporaryPassword}
                    onChange={event => setTemporaryPassword(event.target.value)}
                    disabled={saving}
                    slotProps={{ input: { endAdornment:
                      <PasswordVisibilityAdornment
                        visible={showTemporaryPassword}
                        onToggle={() => setShowTemporaryPassword(value => !value)}
                      /> } }}
                  />
                  <TextField
                    required
                    type={showNewPassword ? 'text' : 'password'}
                    label="Nova senha"
                    autoComplete="new-password"
                    value={newPassword}
                    onChange={event => setNewPassword(event.target.value)}
                    disabled={saving}
                    slotProps={{ input: { endAdornment:
                      <PasswordVisibilityAdornment
                        visible={showNewPassword}
                        onToggle={() => setShowNewPassword(value => !value)}
                      /> } }}
                    helperText="Ao menos 8 caracteres, com maiúscula, minúscula e número."
                  />
                  <TextField
                    required
                    type={showConfirmation ? 'text' : 'password'}
                    label="Confirmar nova senha"
                    autoComplete="new-password"
                    value={confirmation}
                    onChange={event => setConfirmation(event.target.value)}
                    disabled={saving}
                    error={Boolean(validationError)}
                    helperText={validationError}
                    slotProps={{ input: { endAdornment:
                      <PasswordVisibilityAdornment
                        visible={showConfirmation}
                        onToggle={() => setShowConfirmation(value => !value)}
                      /> } }}
                  />
                  <Button
                    type="submit"
                    variant="contained"
                    disabled={
                      saving
                      || !email.trim()
                      || !temporaryPassword
                      || !newPassword
                      || !confirmation
                      || Boolean(validationError)
                    }
                    startIcon={saving
                      ? <CircularProgress size={18} color="inherit" />
                      : <LockResetRoundedIcon />}
                  >
                    {saving ? 'Atualizando…' : 'Atualizar senha'}
                  </Button>
        </Stack>
      </Box>
    </AuthShell>
  )
}
