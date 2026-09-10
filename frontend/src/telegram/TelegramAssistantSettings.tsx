import { useEffect, useState } from 'react'
import { Alert, Button, CircularProgress, Dialog, DialogActions, DialogContent,
  DialogTitle, Link, Stack, Typography } from '@mui/material'
import { getErrorMessage } from '../services/api'
import { createTelegramLinkCode, getTelegramStatus, unlinkTelegram,
  type TelegramLinkCode, type TelegramStatus } from './api'

export function TelegramAssistantSettings({ open, onClose }: { open: boolean; onClose: () => void }) {
  const [status, setStatus] = useState<TelegramStatus | null>(null)
  const [code, setCode] = useState<TelegramLinkCode | null>(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const load = async () => {
    setError(null)
    try { setStatus(await getTelegramStatus()) } catch (reason) { setError(getErrorMessage(reason)) }
  }
  useEffect(() => { if (open) void load() }, [open])
  const generate = async () => {
    setBusy(true); setError(null)
    try { setCode(await createTelegramLinkCode()) } catch (reason) { setError(getErrorMessage(reason)) }
    finally { setBusy(false) }
  }
  const unlink = async () => {
    setBusy(true); setError(null)
    try { await unlinkTelegram(); setCode(null); await load() } catch (reason) { setError(getErrorMessage(reason)) }
    finally { setBusy(false) }
  }
  return <Dialog open={open} onClose={busy ? undefined : onClose} fullWidth maxWidth="xs">
    <DialogTitle>Assistente no Telegram</DialogTitle>
    <DialogContent><Stack spacing={2} pt={0.5}>
      {!status && !error && <CircularProgress size={28} aria-label="Carregando Telegram" />}
      {error && <Alert severity="error">{error}</Alert>}
      {status && !status.enabled && <Alert severity="info">O Assistente no Telegram está desativado.</Alert>}
      {status?.linked ? <>
        <Alert severity="success">Telegram conectado</Alert>
        {status.activeCondominiumName && <Typography variant="body2">Condomínio atual: <b>{status.activeCondominiumName}</b></Typography>}
        {status.linkedAt && <Typography variant="caption" color="text.secondary">Vinculado em {new Date(status.linkedAt).toLocaleDateString('pt-BR')}.</Typography>}
      </> : status?.enabled && <>
        <Typography color="text.secondary">Converse com o Assistente do Comvy diretamente pelo Telegram.</Typography>
        {code && <Alert severity="info">
          Envie <b>/start {code.code}</b> ao bot. O código expira em 10 minutos e só pode ser usado uma vez.
        </Alert>}
        {code?.deepLink && <Link href={code.deepLink} target="_blank" rel="noopener noreferrer">Abrir bot no Telegram</Link>}
      </>}
    </Stack></DialogContent>
    <DialogActions>
      <Button onClick={onClose} disabled={busy}>Fechar</Button>
      {status?.linked && <Button color="error" onClick={() => void unlink()} disabled={busy}>Desvincular</Button>}
      {status?.enabled && !status.linked && <Button variant="contained" onClick={() => void generate()} disabled={busy}>
        {busy ? 'Gerando…' : 'Vincular Telegram'}
      </Button>}
    </DialogActions>
  </Dialog>
}
