import { useRef, useState, type FormEvent } from 'react'
import { Alert, Box, Button, CircularProgress, Stack, TextField, Typography } from '@mui/material'
import LockResetRoundedIcon from '@mui/icons-material/LockResetRounded'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { PasswordVisibilityAdornment } from '../components/PasswordVisibilityAdornment'
import { AuthShell } from '../layout/AuthShell'
import { api } from '../services/api'

type ResetState = 'form' | 'invalid-link' | 'success'
type ApiFailure = { response?: { status?: number; data?: { error?: unknown; requirements?: unknown } } }

const passwordRequirements = 'Ao menos 8 caracteres, com maiúscula, minúscula e número.'
const invalidLinkMessage = 'Este link de redefinição não é mais válido. Solicite um novo link.'
const transportError = 'Não foi possível processar sua solicitação agora. Tente novamente.'

function isPasswordRejected(error: ApiFailure) {
  const message = error.response?.data?.error
  return error.response?.status === 400
    && typeof message === 'string'
    && message.toLowerCase().includes('não atende aos requisitos')
}

function isInvalidLink(error: ApiFailure) {
  const message = error.response?.data?.error
  return error.response?.status === 400
    && typeof message === 'string'
    && message.toLowerCase().includes('link é inválido')
}

export function ResetPasswordPage() {
  const navigate = useNavigate()
  const [params] = useSearchParams()
  const userId = params.get('userId') ?? ''
  const token = params.get('token') ?? ''
  const [state, setState] = useState<ResetState>(userId && token ? 'form' : 'invalid-link')
  const [invalidated, setInvalidated] = useState(false)
  const [password, setPassword] = useState('')
  const [confirmation, setConfirmation] = useState('')
  const [error, setError] = useState('')
  const [saving, setSaving] = useState(false)
  const [showPassword, setShowPassword] = useState(false)
  const [showConfirmation, setShowConfirmation] = useState(false)
  const successHeading = useRef<HTMLHeadingElement>(null)
  const invalidHeading = useRef<HTMLHeadingElement>(null)

  const validationError = password.length > 0 && password.length < 8
    ? 'A nova senha deve possuir ao menos 8 caracteres.'
    : confirmation.length > 0 && password !== confirmation
      ? 'A confirmação da senha não confere.'
      : ''

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    if (saving || validationError || !password || !confirmation) return
    setError('')
    setSaving(true)
    try {
      await api.post('/auth/reset-password', { userId, token, password, confirmation })
      setState('success')
      navigate('/reset-password', { replace: true })
      requestAnimationFrame(() => successHeading.current?.focus())
    } catch (requestError) {
      const failure = requestError as ApiFailure
      if (isPasswordRejected(failure)) setError(passwordRequirements)
      else if (isInvalidLink(failure)) {
        setInvalidated(true)
        setState('invalid-link')
        requestAnimationFrame(() => invalidHeading.current?.focus())
      }
      else setError(transportError)
    } finally {
      setSaving(false)
    }
  }

  return (
    <AuthShell>
      {state === 'invalid-link' ? (
        <Stack spacing={2}>
          <Typography ref={invalidHeading} variant="h1" tabIndex={-1}>Link inválido</Typography>
          <Typography color="text.secondary">{invalidated ? invalidLinkMessage : 'Este link de redefinição não é válido. Solicite um novo link para continuar.'}</Typography>
          <Button variant="contained" component={Link} to="/forgot-password">Solicitar novo link</Button>
          <Button component={Link} to="/login" color="inherit">Voltar para o login</Button>
        </Stack>
      ) : state === 'success' ? (
        <Stack spacing={2}>
          <Typography ref={successHeading} variant="h1" tabIndex={-1}>Senha redefinida</Typography>
          <Typography color="text.secondary">Sua nova senha já pode ser usada para entrar no Comvy.</Typography>
          <Button variant="contained" component={Link} to="/login">Entrar</Button>
        </Stack>
      ) : (
        <>
          <Stack spacing={1} mb={4}>
            <Typography variant="h1">Defina uma nova senha</Typography>
            <Typography color="text.secondary">Escolha uma nova senha para acessar o Comvy.</Typography>
          </Stack>
          <Box component="form" onSubmit={submit} noValidate>
            <Stack spacing={2.25}>
              {error && <Alert severity="error">{error}</Alert>}
              <TextField label="Nova senha" type={showPassword ? 'text' : 'password'} autoComplete="new-password" required fullWidth value={password} onChange={(event) => setPassword(event.target.value)} disabled={saving} inputProps={{ minLength: 8 }} helperText={passwordRequirements} slotProps={{ input: { endAdornment: <PasswordVisibilityAdornment visible={showPassword} onToggle={() => setShowPassword(value => !value)} /> } }} />
              <TextField label="Confirmar nova senha" type={showConfirmation ? 'text' : 'password'} autoComplete="new-password" required fullWidth value={confirmation} onChange={(event) => setConfirmation(event.target.value)} disabled={saving} error={Boolean(validationError)} helperText={validationError} slotProps={{ input: { endAdornment: <PasswordVisibilityAdornment visible={showConfirmation} onToggle={() => setShowConfirmation(value => !value)} /> } }} />
              <Button type="submit" variant="contained" size="large" disabled={saving || !password || !confirmation || Boolean(validationError)} startIcon={saving ? <CircularProgress size={18} color="inherit" /> : <LockResetRoundedIcon />}>
                {saving ? 'Redefinindo…' : 'Redefinir senha'}
              </Button>
              <Button component={Link} to="/login" color="inherit">Voltar para o login</Button>
            </Stack>
          </Box>
        </>
      )}
    </AuthShell>
  )
}
