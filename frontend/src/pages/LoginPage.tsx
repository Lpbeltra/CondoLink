import { useState, type FormEvent } from 'react'
import { Alert, Box, Button, Card, CardContent, CircularProgress, Container, Stack, TextField, Typography } from '@mui/material'
import LoginRoundedIcon from '@mui/icons-material/LoginRounded'
import { Navigate, useLocation, useNavigate } from 'react-router-dom'
import { Brand } from '../components/Brand'
import { useAuth } from '../auth/AuthContext'
import { getErrorMessage } from '../services/api'

export function LoginPage() {
  const { user, login } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)

  if (user) return <Navigate to="/" replace />

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault()
    setError('')
    setIsSubmitting(true)
    try {
      await login(email.trim(), password)
      const destination = (location.state as { from?: string } | null)?.from || '/'
      navigate(destination, { replace: true })
    } catch (requestError) {
      setError(getErrorMessage(requestError))
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <Box minHeight="100dvh" display="grid" sx={{ placeItems: 'center', py: 4, background: 'radial-gradient(circle at 15% 15%, rgba(31,94,255,.12), transparent 28%), radial-gradient(circle at 90% 85%, rgba(114,89,217,.09), transparent 32%)' }}>
      <Container maxWidth="xs">
        <Stack alignItems="center" mb={4}><Brand /></Stack>
        <Card>
          <CardContent sx={{ p: { xs: 3, sm: 4.5 }, '&:last-child': { pb: { xs: 3, sm: 4.5 } } }}>
            <Typography variant="h1">Que bom ter você aqui.</Typography>
            <Typography color="text.secondary" mt={1}>Acesse seu condomínio de forma simples e segura.</Typography>
            <Box component="form" onSubmit={handleSubmit} mt={4} noValidate>
              <Stack spacing={2.25}>
                {error && <Alert severity="error" onClose={() => setError('')}>{error}</Alert>}
                <TextField label="E-mail" type="email" autoComplete="email" autoFocus required fullWidth value={email} onChange={(event) => setEmail(event.target.value)} disabled={isSubmitting} />
                <TextField label="Senha" type="password" autoComplete="current-password" required fullWidth value={password} onChange={(event) => setPassword(event.target.value)} disabled={isSubmitting} />
                <Button type="submit" variant="contained" size="large" disabled={isSubmitting || !email || !password} startIcon={isSubmitting ? <CircularProgress size={18} color="inherit" /> : <LoginRoundedIcon />}>
                  {isSubmitting ? 'Entrando…' : 'Entrar'}
                </Button>
              </Stack>
            </Box>
          </CardContent>
        </Card>
        <Typography textAlign="center" color="text.secondary" fontSize=".8rem" mt={3}>Seu condomínio mais conectado.</Typography>
      </Container>
    </Box>
  )
}
