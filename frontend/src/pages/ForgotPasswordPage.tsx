import { useRef, useState, type FormEvent } from 'react'
import { Alert, Box, Button, CircularProgress, Stack, TextField, Typography } from '@mui/material'
import { Link } from 'react-router-dom'
import { api } from '../services/api'
import { AuthShell } from '../layout/AuthShell'

const transportError = 'Não foi possível processar sua solicitação agora. Tente novamente.'

export function ForgotPasswordPage() {
  const [email, setEmail] = useState('')
  const [error, setError] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [confirmed, setConfirmed] = useState(false)
  const confirmationHeading = useRef<HTMLHeadingElement>(null)

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    setError('')
    const normalizedEmail = email.trim()
    if (!normalizedEmail) return setError('Informe seu e-mail.')
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(normalizedEmail)) return setError('Informe um endereço de e-mail válido.')

    setIsSubmitting(true)
    try {
      await api.post('/auth/forgot-password', { email: normalizedEmail })
      setConfirmed(true)
      requestAnimationFrame(() => confirmationHeading.current?.focus())
    } catch {
      setError(transportError)
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <AuthShell>
      {confirmed ? (
        <Stack spacing={2}>
          <Typography ref={confirmationHeading} variant="h1" tabIndex={-1}>Verifique seu e-mail</Typography>
          <Typography color="text.secondary">Se existir uma conta elegível para este e-mail, enviaremos as instruções para redefinir sua senha.</Typography>
          <Typography color="text.secondary">Verifique também sua caixa de spam.</Typography>
          <Button variant="contained" component={Link} to="/login">Voltar para o login</Button>
        </Stack>
      ) : (
        <>
          <Stack spacing={1} mb={4}>
            <Typography variant="h1">Esqueceu sua senha?</Typography>
            <Typography color="text.secondary">Informe seu e-mail e enviaremos as instruções para redefinir sua senha.</Typography>
          </Stack>
          <Box component="form" onSubmit={submit} noValidate>
            <Stack spacing={2.25}>
              {error && <Alert severity="error">{error}</Alert>}
              <TextField label="E-mail" type="email" autoComplete="email" autoFocus required fullWidth value={email} onChange={(event) => setEmail(event.target.value)} disabled={isSubmitting} />
              <Button type="submit" variant="contained" size="large" disabled={isSubmitting} startIcon={isSubmitting ? <CircularProgress size={18} color="inherit" /> : undefined}>
                {isSubmitting ? 'Enviando…' : 'Enviar instruções'}
              </Button>
              <Button component={Link} to="/login" color="inherit">Voltar para o login</Button>
            </Stack>
          </Box>
        </>
      )}
    </AuthShell>
  )
}
