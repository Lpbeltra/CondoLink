import { useState, type FormEvent } from 'react'
import { Alert, Box, Button, CircularProgress, Stack, TextField, Typography } from '@mui/material'
import LoginRoundedIcon from '@mui/icons-material/LoginRounded'
import { Link, Navigate, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { authError } from '../auth/errors'
import { authenticatedEntryPath } from '../auth/routeAccess'
import { PasswordVisibilityAdornment } from '../components/PasswordVisibilityAdornment'
import { AuthShell } from '../layout/AuthShell'

export function LoginPage() {
  const { user, login } = useAuth()
  const navigate = useNavigate()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [showPassword, setShowPassword] = useState(false)

  if (user) return <Navigate to="/" replace />

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault()
    setError('')
    if (!email.trim()) return setError('Informe seu e-mail.')
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim())) return setError('Informe um endereço de e-mail válido.')
    if (!password) return setError('Informe sua senha.')
    setIsSubmitting(true)
    try {
      const outcome = await login(email.trim(), password)
      if (outcome.requiresPasswordChange) {
        navigate('/change-password', {
          replace: true,
          state: {
            email: outcome.email,
            temporaryPassword: outcome.temporaryPassword,
          },
        })
        return
      }
      navigate(authenticatedEntryPath, { replace: true })
    } catch (requestError) {
      setError(authError(requestError))
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <AuthShell>
      <Stack spacing={1} mb={4}>
        <Typography variant="h1">Entrar no Comvy</Typography>
        <Typography color="text.secondary">Use seus dados de acesso para continuar.</Typography>
      </Stack>
      <Box component="form" onSubmit={handleSubmit} noValidate>
        <Stack spacing={2.25}>
          {error && <Alert severity="error" onClose={() => setError('')}>{error}</Alert>}
          <TextField label="E-mail" type="email" autoComplete="email" autoFocus required fullWidth value={email} onChange={(event) => setEmail(event.target.value)} disabled={isSubmitting} />
          <TextField label="Senha" type={showPassword ? 'text' : 'password'} autoComplete="current-password" required fullWidth value={password} onChange={(event) => setPassword(event.target.value)} disabled={isSubmitting} slotProps={{ input: { endAdornment: <PasswordVisibilityAdornment visible={showPassword} onToggle={() => setShowPassword(value => !value)} /> } }} />
          <Button component={Link} to="/forgot-password" color="inherit" size="small" sx={{ alignSelf: 'flex-start', mt: -1 }}>
            Esqueci minha senha
          </Button>
          <Button type="submit" variant="contained" size="large" disabled={isSubmitting} startIcon={isSubmitting ? <CircularProgress size={18} color="inherit" /> : <LoginRoundedIcon />}>
            {isSubmitting ? 'Entrando…' : 'Entrar'}
          </Button>
        </Stack>
      </Box>
    </AuthShell>
  )
}
