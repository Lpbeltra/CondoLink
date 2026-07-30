import { useState, type FormEvent } from 'react'
import { Alert, Box, Button, Card, CardContent, CircularProgress, Container, IconButton, InputAdornment, Stack, TextField, Typography } from '@mui/material'
import LoginRoundedIcon from '@mui/icons-material/LoginRounded'
import VisibilityRoundedIcon from '@mui/icons-material/VisibilityRounded'
import VisibilityOffRoundedIcon from '@mui/icons-material/VisibilityOffRounded'
import { Navigate, useNavigate } from 'react-router-dom'
import { Brand } from '../components/Brand'
import { useAuth } from '../auth/AuthContext'
import { authError } from '../auth/errors'
import { authenticatedEntryPath } from '../auth/routeAccess'
import { ThemeModeToggle } from '../theme/ThemeModeToggle'

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
    <Box minHeight="100dvh" display="grid" sx={{ placeItems: 'center', py: 4, background: 'radial-gradient(circle at 15% 15%, rgba(31,94,255,.12), transparent 28%), radial-gradient(circle at 90% 85%, rgba(114,89,217,.09), transparent 32%)' }}>
      <Box sx={{ position: 'fixed', top: 12, right: 12 }}><ThemeModeToggle /></Box>
      <Container maxWidth="xs">
        <Stack alignItems="center" mb={3}><Brand /></Stack>
        <Card>
          <CardContent sx={{ p: { xs: 3, sm: 4.5 }, '&:last-child': { pb: { xs: 3, sm: 4.5 } } }}>
            <Typography variant="h3" textAlign="center">Comunicação clara!</Typography>
            <Typography color="text.secondary" textAlign="center" mt={1}>Centralizando solicitações e informações</Typography>
            <Box component="form" onSubmit={handleSubmit} mt={4} noValidate>
              <Stack spacing={2.25}>
                {error && <Alert severity="error" onClose={() => setError('')}>{error}</Alert>}
                <TextField label="E-mail" type="email" autoComplete="email" autoFocus required fullWidth value={email} onChange={(event) => setEmail(event.target.value)} disabled={isSubmitting} />
                <TextField label="Senha" type={showPassword ? 'text' : 'password'} autoComplete="current-password" required fullWidth value={password} onChange={(event) => setPassword(event.target.value)} disabled={isSubmitting} slotProps={{ input: { endAdornment: <InputAdornment position="end"><IconButton edge="end" aria-label={showPassword ? 'Ocultar senha' : 'Mostrar senha'} onClick={() => setShowPassword(value => !value)}>{showPassword ? <VisibilityOffRoundedIcon /> : <VisibilityRoundedIcon />}</IconButton></InputAdornment> } }} />
                <Button type="submit" variant="contained" size="large" disabled={isSubmitting} startIcon={isSubmitting ? <CircularProgress size={18} color="inherit" /> : <LoginRoundedIcon />}>
                  {isSubmitting ? 'Entrando…' : 'Entrar'}
                </Button>
              </Stack>
            </Box>
          </CardContent>
        </Card>
        <Typography textAlign="center" color="text.secondary" fontSize=".8rem" mt={3}>Menos ruído. Mais contexto.</Typography>
      </Container>
    </Box>
  )
}
