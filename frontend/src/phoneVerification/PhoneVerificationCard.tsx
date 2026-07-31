import { useEffect, useState } from 'react'
import { Alert, Button, Card, CardContent, Chip, CircularProgress, Stack, TextField, Typography } from '@mui/material'
import axios from 'axios'
import { getErrorMessage } from '../services/api'
import {
  confirmPhoneVerification,
  getPhoneVerificationStatus,
  startPhoneVerification,
  type PhoneVerificationStatus,
} from './api'

const formatExpiration = (value: string) =>
  new Intl.DateTimeFormat('pt-BR', {
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(value))

const messageFor = (error: unknown) =>
  axios.isAxiosError<{ error?: string }>(error) && error.response?.data.error
    ? error.response.data.error
    : getErrorMessage(error)

export function PhoneVerificationCard() {
  const [status, setStatus] = useState<PhoneVerificationStatus | null>(null)
  const [loading, setLoading] = useState(true)
  const [sending, setSending] = useState(false)
  const [confirming, setConfirming] = useState(false)
  const [code, setCode] = useState('')
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')

  const load = async () => {
    try {
      const nextStatus = await getPhoneVerificationStatus()
      setStatus(nextStatus)
      setError('')
      return nextStatus
    } catch (requestError) {
      setError(messageFor(requestError))
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => { void load() }, [])

  const start = async () => {
    setSending(true)
    setError('')
    setSuccess('')
    try {
      const result = await startPhoneVerification()
      setSuccess(result.status === 'already_confirmed'
        ? 'Este telefone já está confirmado.'
        : 'Código enviado. Digite abaixo o código recebido pelo WhatsApp.')
      const nextStatus = await load()
      if (nextStatus) window.dispatchEvent(new CustomEvent(
        'condolink:phone-verification-updated',
        { detail: nextStatus },
      ))
    } catch (requestError) {
      setError(messageFor(requestError))
    } finally {
      setSending(false)
    }
  }

  const confirm = async () => {
    setConfirming(true)
    setError('')
    setSuccess('')
    try {
      await confirmPhoneVerification(code)
      setCode('')
      setSuccess('Telefone confirmado com sucesso.')
      const nextStatus = await load()
      if (nextStatus) window.dispatchEvent(new CustomEvent(
        'condolink:phone-verification-updated',
        { detail: nextStatus },
      ))
    } catch (requestError) {
      setError(messageFor(requestError))
    } finally {
      setConfirming(false)
    }
  }

  return (
    <Card elevation={0} sx={{ mt: 2 }}>
      <CardContent>
        <Stack gap={1.5} alignItems="flex-start">
          <Typography variant="h3">Telefone e WhatsApp</Typography>
          {loading ? <CircularProgress size={24} aria-label="Carregando telefone" /> : (
            <>
              <Typography color="text.secondary">
                {status?.maskedPhoneNumber ?? 'Nenhum telefone cadastrado'}
              </Typography>
              <Chip
                color={status?.confirmed ? 'success' : 'default'}
                label={status?.confirmed ? 'Confirmado' : 'Não confirmado'}
                size="small"
              />
              {status?.activeChallenge && status.expiresAt && (
                <Typography color="text.secondary">
                  Código válido até {formatExpiration(status.expiresAt)}.
                </Typography>
              )}
              {status?.activeChallenge && !status.confirmed && (
                <Stack
                  direction={{ xs: 'column', sm: 'row' }}
                  gap={1}
                  alignItems={{ sm: 'flex-start' }}
                  width="100%"
                >
                  <TextField
                    label="Código de confirmação"
                    value={code}
                    onChange={(event) => setCode(
                      event.target.value.replace(/\D/g, '').slice(0, 6))}
                    disabled={confirming}
                    autoComplete="one-time-code"
                    slotProps={{
                      htmlInput: {
                        inputMode: 'numeric',
                        pattern: '[0-9]*',
                        maxLength: 6,
                        'aria-label': 'Código de confirmação',
                      },
                    }}
                  />
                  <Button
                    variant="contained"
                    onClick={() => void confirm()}
                    disabled={confirming || code.length !== 6}
                  >
                    {confirming ? 'Confirmando…' : 'Confirmar código'}
                  </Button>
                </Stack>
              )}
              {!status?.confirmed && status?.canResendAt && (
                <Typography color="text.secondary">
                  Aguarde até {formatExpiration(status.canResendAt)} para reenviar.
                </Typography>
              )}
              {!status?.confirmed && (
                <Button
                  variant="contained"
                  onClick={() => void start()}
                  disabled={sending || !status?.maskedPhoneNumber || !status?.canResend}
                >
                  {sending ? 'Enviando…'
                    : status?.activeChallenge ? 'Reenviar código'
                      : 'Confirmar pelo WhatsApp'}
                </Button>
              )}
            </>
          )}
          {success && <Alert severity="success">{success}</Alert>}
          {error && <Alert severity="error">{error}</Alert>}
        </Stack>
      </CardContent>
    </Card>
  )
}
