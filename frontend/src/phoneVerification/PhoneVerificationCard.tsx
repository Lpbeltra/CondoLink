import { useEffect, useState } from 'react'
import { Alert, Button, Card, CardContent, Chip, CircularProgress, Stack, Typography } from '@mui/material'
import axios from 'axios'
import { getErrorMessage } from '../services/api'
import { getPhoneVerificationStatus, startPhoneVerification, type PhoneVerificationStatus } from './api'

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
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')

  const load = async () => {
    try {
      setStatus(await getPhoneVerificationStatus())
      setError('')
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
        : 'Código enviado. Responda pelo WhatsApp com o código recebido.')
      await load()
    } catch (requestError) {
      setError(messageFor(requestError))
    } finally {
      setSending(false)
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
