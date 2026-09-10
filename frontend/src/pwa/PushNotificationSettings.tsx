import { useCallback, useEffect, useState } from 'react'
import NotificationsActiveRoundedIcon from '@mui/icons-material/NotificationsActiveRounded'
import NotificationsOffRoundedIcon from '@mui/icons-material/NotificationsOffRounded'
import { Alert, Button, CircularProgress, Dialog, DialogActions, DialogContent,
  DialogTitle, Stack, Typography } from '@mui/material'
import { AppleInstallInstructions } from './AppleInstallInstructions'
import { usePwaDisplayMode } from './pwaDisplayMode'
import { activateWebPush, currentPushSubscription, deactivateWebPush,
  getWebPushConfig, supportsWebPush, type WebPushConfig } from './webPush'

type State = 'loading' | 'disabled' | 'install-ios' | 'unsupported' | 'available'
  | 'active' | 'denied' | 'error'

export function PushNotificationSettings({ open, onClose }: { open: boolean; onClose: () => void }) {
  const mode = usePwaDisplayMode()
  const [state, setState] = useState<State>('loading')
  const [config, setConfig] = useState<WebPushConfig | null>(null)
  const [subscription, setSubscription] = useState<PushSubscription | null>(null)
  const [busy, setBusy] = useState(false)

  const load = useCallback(async () => {
    setState('loading')
    try {
      const serverConfig = await getWebPushConfig()
      setConfig(serverConfig)
      if (!serverConfig.enabled || !serverConfig.vapidPublicKey) return setState('disabled')
      if (mode.isIos && !mode.isStandalone) return setState('install-ios')
      if (!supportsWebPush()) return setState('unsupported')
      if (Notification.permission === 'denied') return setState('denied')
      const current = await currentPushSubscription()
      setSubscription(current)
      setState(current ? 'active' : 'available')
    } catch (error) {
      console.warn('[PWA] Não foi possível consultar Web Push.', error)
      setState('error')
    }
  }, [mode.isIos, mode.isStandalone])

  useEffect(() => { if (open) void load() }, [load, open])

  const activate = async () => {
    if (busy || !config?.vapidPublicKey) return
    setBusy(true)
    try {
      const permission = Notification.permission === 'default'
        ? await Notification.requestPermission() : Notification.permission
      if (permission !== 'granted') return setState(permission === 'denied' ? 'denied' : 'available')
      const current = await activateWebPush(config.vapidPublicKey)
      setSubscription(current)
      setState('active')
    } catch (error) {
      console.warn('[PWA] Não foi possível ativar Web Push.', error)
      setState('error')
    } finally { setBusy(false) }
  }

  const deactivate = async () => {
    if (busy || !subscription) return
    setBusy(true)
    try {
      await deactivateWebPush(subscription)
      setSubscription(null)
      setState('available')
    } catch (error) {
      console.warn('[PWA] Não foi possível desativar Web Push.', error)
      setState('error')
    } finally { setBusy(false) }
  }

  return <Dialog open={open} onClose={busy ? undefined : onClose} fullWidth maxWidth="xs">
    <DialogTitle>Notificações</DialogTitle>
    <DialogContent>
      <Stack spacing={2} pt={0.5}>
        <Typography color="text.secondary">
          Receba avisos importantes do Comvy neste dispositivo.
        </Typography>
        {state === 'loading' && <CircularProgress size={28} aria-label="Carregando notificações" />}
        {state === 'disabled' && <Alert severity="info">Notificações estão desativadas no servidor.</Alert>}
        {state === 'install-ios' && <Alert severity="info" icon={false}>
          <AppleInstallInstructions isIpad={mode.isIpad} />
        </Alert>}
        {state === 'unsupported' && <Alert severity="info">Este navegador não oferece notificações Web Push.</Alert>}
        {state === 'denied' && <Alert severity="warning">As notificações estão bloqueadas neste navegador. Você pode liberá-las nas configurações do dispositivo.</Alert>}
        {state === 'error' && <Alert severity="error">Não foi possível concluir agora. Tente novamente.</Alert>}
        {state === 'active' && <Alert icon={<NotificationsActiveRoundedIcon />} severity="success">Notificações ativadas neste dispositivo.</Alert>}
        {state === 'available' && <Alert severity="info">Ative quando quiser. A permissão só será solicitada após seu clique.</Alert>}
      </Stack>
    </DialogContent>
    <DialogActions>
      <Button onClick={onClose} disabled={busy}>Fechar</Button>
      {state === 'error' && <Button onClick={() => void load()} disabled={busy}>Tentar novamente</Button>}
      {state === 'available' && <Button variant="contained" onClick={() => void activate()} disabled={busy}>Ativar notificações</Button>}
      {state === 'active' && <Button color="inherit" startIcon={<NotificationsOffRoundedIcon />} onClick={() => void deactivate()} disabled={busy}>Desativar neste dispositivo</Button>}
    </DialogActions>
  </Dialog>
}
