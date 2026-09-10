import InstallMobileRoundedIcon from '@mui/icons-material/InstallMobileRounded'
import { Box, Button, Paper, Stack, Typography } from '@mui/material'
import { useEffect, useState } from 'react'
import { AppleInstallInstructions } from './AppleInstallInstructions'
import { readPwaDisplayMode, usePwaDisplayMode } from './pwaDisplayMode'

const dismissalKey = 'comvy.pwaInstallDismissedAt'
const dismissalDurationMs = 30 * 24 * 60 * 60 * 1000

interface BeforeInstallPromptEvent extends Event {
  prompt(): Promise<void>
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed'; platform: string }>
}

function recentlyDismissed() {
  try {
    const dismissedAt = Number(localStorage.getItem(dismissalKey))
    return Number.isFinite(dismissedAt) && Date.now() - dismissedAt < dismissalDurationMs
  } catch {
    return false
  }
}

export function PwaInstallBanner() {
  const displayMode = usePwaDisplayMode()
  const [installPrompt, setInstallPrompt] = useState<BeforeInstallPromptEvent | null>(null)
  const [hidden, setHidden] = useState(() => readPwaDisplayMode().isStandalone || recentlyDismissed())
  const ios = displayMode.isIosInstallable

  useEffect(() => {
    if (displayMode.isStandalone) setHidden(true)
  }, [displayMode.isStandalone])

  useEffect(() => {
    const onBeforeInstallPrompt = (event: Event) => {
      event.preventDefault()
      setInstallPrompt(event as BeforeInstallPromptEvent)
      if (!readPwaDisplayMode().isStandalone && !recentlyDismissed()) setHidden(false)
    }
    const onInstalled = () => {
      setInstallPrompt(null)
      setHidden(true)
    }
    window.addEventListener('beforeinstallprompt', onBeforeInstallPrompt)
    window.addEventListener('appinstalled', onInstalled)
    return () => {
      window.removeEventListener('beforeinstallprompt', onBeforeInstallPrompt)
      window.removeEventListener('appinstalled', onInstalled)
    }
  }, [])

  if (hidden || (!ios && !installPrompt)) return null

  const dismiss = () => {
    try { localStorage.setItem(dismissalKey, String(Date.now())) } catch { /* optional preference */ }
    setHidden(true)
  }

  const install = async () => {
    if (!installPrompt) return
    await installPrompt.prompt()
    const choice = await installPrompt.userChoice
    setInstallPrompt(null)
    if (choice.outcome === 'accepted') setHidden(true)
    else dismiss()
  }

  return (
    <Box px={{ xs: 2, sm: 3 }} pt={2} maxWidth={1440} mx="auto" width="100%">
      <Paper variant="outlined" role="region" aria-label="Instalar Comvy" sx={{ p: 2, maxWidth: { md: 680 } }}>
        <Stack direction="row" gap={1.5} alignItems="flex-start">
          <InstallMobileRoundedIcon color="primary" sx={{ mt: 0.25 }} />
          <Box flex={1} minWidth={0}>
            {ios ? <AppleInstallInstructions isIpad={displayMode.isIpad} /> : <>
              <Typography fontWeight={750}>
                {displayMode.isAndroid ? 'Instale o Comvy' : 'Instale o Comvy neste computador'}
              </Typography>
              <Typography variant="body2" color="text.secondary">
                {displayMode.isAndroid
                  ? 'Tenha acesso mais rápido ao Comvy pela tela inicial do seu dispositivo.'
                  : 'Abra o Comvy como aplicativo neste computador.'}
              </Typography>
            </>}
            <Stack direction="row" gap={1} mt={1.5} flexWrap="wrap">
              {!ios && <Button size="small" variant="contained" onClick={() => void install()}>Instalar Comvy</Button>}
              <Button size="small" color="inherit" onClick={dismiss}>Agora não</Button>
            </Stack>
          </Box>
        </Stack>
      </Paper>
    </Box>
  )
}
