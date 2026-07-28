import { useState, type FormEvent } from 'react'
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  CircularProgress,
  Container,
  Stack,
  TextField,
  Typography,
} from '@mui/material'
import LockResetRoundedIcon from '@mui/icons-material/LockResetRounded'
import { useLocation, useNavigate } from 'react-router-dom'
import { Brand } from '../components/Brand'
import { api } from '../services/api'
import { authError } from '../auth/errors'
import { useAuth } from '../auth/AuthContext'
import { authenticatedEntryPath } from '../auth/routeAccess'
import { ThemeModeToggle } from '../theme/ThemeModeToggle'

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
    <Box minHeight="100dvh" display="grid" sx={{ placeItems: 'center', py: 4 }}>
      <Box sx={{ position: 'fixed', top: 12, right: 12 }}>
        <ThemeModeToggle />
      </Box>
      <Container maxWidth="xs">
        <Stack alignItems="center" mb={4}><Brand /></Stack>
        <Card>
          <CardContent sx={{ p: { xs: 3, sm: 4.5 } }}>
            <Typography variant="h1">Alterar senha</Typography>
            <Typography color="text.secondary" mt={1}>
              Você precisa alterar sua senha temporária antes de continuar.
            </Typography>
            <Box component="form" onSubmit={event => void submit(event)} mt={3}>
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
                    type="password"
                    label="Senha temporária"
                    autoComplete="current-password"
                    value={temporaryPassword}
                    onChange={event => setTemporaryPassword(event.target.value)}
                    disabled={saving}
                  />
                  <TextField
                    required
                    type="password"
                    label="Nova senha"
                    autoComplete="new-password"
                    value={newPassword}
                    onChange={event => setNewPassword(event.target.value)}
                    disabled={saving}
                    helperText="Ao menos 8 caracteres, com maiúscula, minúscula e número."
                  />
                  <TextField
                    required
                    type="password"
                    label="Confirmar nova senha"
                    autoComplete="new-password"
                    value={confirmation}
                    onChange={event => setConfirmation(event.target.value)}
                    disabled={saving}
                    error={Boolean(validationError)}
                    helperText={validationError}
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
          </CardContent>
        </Card>
      </Container>
    </Box>
  )
}
