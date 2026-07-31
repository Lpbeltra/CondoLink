import { useEffect, useState, type FormEvent } from 'react'
import { Alert, Box, Button, Card, CardContent, CircularProgress, Container, IconButton, InputAdornment, Stack, TextField, Typography } from '@mui/material'
import LoginRoundedIcon from '@mui/icons-material/LoginRounded'
import WhatsAppIcon from '@mui/icons-material/WhatsApp'
import VisibilityRoundedIcon from '@mui/icons-material/VisibilityRounded'
import VisibilityOffRoundedIcon from '@mui/icons-material/VisibilityOffRounded'
import { Navigate, useNavigate } from 'react-router-dom'
import { Brand } from '../components/Brand'
import { useAuth } from '../auth/AuthContext'
import { authError, whatsAppAuthError } from '../auth/errors'
import { authenticatedEntryPath } from '../auth/routeAccess'
import { ThemeModeToggle } from '../theme/ThemeModeToggle'

export function LoginPage() {
  const {
    user,
    login,
    requestWhatsAppCode,
    loginWithWhatsApp,
  } = useAuth()
  const navigate = useNavigate()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [showPassword, setShowPassword] = useState(false)
  const [mode, setMode] = useState<'password' | 'whatsapp'>('password')
  const [phoneNumber, setPhoneNumber] = useState('')
  const [code, setCode] = useState('')
  const [codeSent, setCodeSent] = useState(false)
  const [cooldown, setCooldown] = useState(0)
  const [success, setSuccess] = useState('')

  useEffect(() => {
    if (cooldown <= 0) return
    const timer = window.setInterval(
      () => setCooldown(value => Math.max(0, value - 1)),
      1000,
    )
    return () => window.clearInterval(timer)
  }, [cooldown])

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

  const requestCode = async (event: { preventDefault(): void }) => {
    event.preventDefault()
    setError('')
    setSuccess('')
    if (!phoneNumber.trim())
      return setError('Informe seu telefone.')
    setIsSubmitting(true)
    try {
      const result = await requestWhatsAppCode(phoneNumber.trim())
      setCodeSent(true)
      setCooldown(result.retryAfterSeconds)
      setSuccess(result.message)
    } catch (requestError) {
      setError(whatsAppAuthError(requestError))
    } finally {
      setIsSubmitting(false)
    }
  }

  const confirmCode = async (event: FormEvent) => {
    event.preventDefault()
    setError('')
    setSuccess('')
    if (code.length !== 6)
      return setError('Informe o código de seis dígitos.')
    setIsSubmitting(true)
    try {
      await loginWithWhatsApp(phoneNumber.trim(), code)
      navigate(authenticatedEntryPath, { replace: true })
    } catch (requestError) {
      setError(whatsAppAuthError(requestError))
    } finally {
      setIsSubmitting(false)
    }
  }

  const selectMode = (nextMode: 'password' | 'whatsapp') => {
    setMode(nextMode)
    setError('')
    setSuccess('')
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
            {mode === 'password' ? (
              <Box component="form" onSubmit={handleSubmit} mt={4} noValidate>
                <Stack spacing={2.25}>
                  {error && <Alert severity="error" onClose={() => setError('')}>{error}</Alert>}
                  <TextField label="E-mail" type="email" autoComplete="email" autoFocus required fullWidth value={email} onChange={(event) => setEmail(event.target.value)} disabled={isSubmitting} />
                  <TextField label="Senha" type={showPassword ? 'text' : 'password'} autoComplete="current-password" required fullWidth value={password} onChange={(event) => setPassword(event.target.value)} disabled={isSubmitting} slotProps={{ input: { endAdornment: <InputAdornment position="end"><IconButton edge="end" aria-label={showPassword ? 'Ocultar senha' : 'Mostrar senha'} onClick={() => setShowPassword(value => !value)}>{showPassword ? <VisibilityOffRoundedIcon /> : <VisibilityRoundedIcon />}</IconButton></InputAdornment> } }} />
                  <Button type="submit" variant="contained" size="large" disabled={isSubmitting} startIcon={isSubmitting ? <CircularProgress size={18} color="inherit" /> : <LoginRoundedIcon />}>
                    {isSubmitting ? 'Entrando…' : 'Entrar'}
                  </Button>
                  <Button
                    type="button"
                    variant="outlined"
                    startIcon={<WhatsAppIcon />}
                    onClick={() => selectMode('whatsapp')}
                    disabled={isSubmitting}
                  >
                    Entrar com WhatsApp
                  </Button>
                </Stack>
              </Box>
            ) : (
              <Box
                component="form"
                onSubmit={codeSent ? confirmCode : requestCode}
                mt={4}
                noValidate
              >
                <Stack spacing={2.25}>
                  {success && <Alert severity="success">{success}</Alert>}
                  {error && <Alert severity="error" onClose={() => setError('')}>{error}</Alert>}
                  <TextField
                    label="Telefone / WhatsApp"
                    autoComplete="tel"
                    autoFocus
                    required
                    fullWidth
                    value={phoneNumber}
                    onChange={(event) => setPhoneNumber(event.target.value)}
                    disabled={isSubmitting || codeSent}
                  />
                  {codeSent && (
                    <TextField
                      label="Código de seis dígitos"
                      autoComplete="one-time-code"
                      required
                      fullWidth
                      value={code}
                      onChange={(event) => setCode(
                        event.target.value.replace(/\D/g, '').slice(0, 6))}
                      disabled={isSubmitting}
                      slotProps={{
                        htmlInput: {
                          inputMode: 'numeric',
                          pattern: '[0-9]*',
                          maxLength: 6,
                        },
                      }}
                    />
                  )}
                  <Button
                    type="submit"
                    variant="contained"
                    size="large"
                    disabled={isSubmitting || (codeSent && code.length !== 6)}
                    startIcon={isSubmitting
                      ? <CircularProgress size={18} color="inherit" />
                      : <WhatsAppIcon />}
                  >
                    {isSubmitting
                      ? codeSent ? 'Confirmando…' : 'Enviando código…'
                      : codeSent ? 'Confirmar e entrar' : 'Enviar código'}
                  </Button>
                  {codeSent && (
                    <Button
                      type="button"
                      variant="text"
                      onClick={(event) => void requestCode(event)}
                      disabled={isSubmitting || cooldown > 0}
                    >
                      {cooldown > 0
                        ? `Aguarde ${cooldown}s para reenviar`
                        : 'Reenviar código'}
                    </Button>
                  )}
                  <Button
                    type="button"
                    variant="outlined"
                    onClick={() => selectMode('password')}
                    disabled={isSubmitting}
                  >
                    Voltar ao login por senha
                  </Button>
                </Stack>
              </Box>
            )}
          </CardContent>
        </Card>
        <Typography textAlign="center" color="text.secondary" fontSize=".8rem" mt={3}>Menos ruído. Mais contexto.</Typography>
      </Container>
    </Box>
  )
}
