import { ThemeProvider } from '@mui/material'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createAppTheme } from '../theme/createAppTheme'
import { PushNotificationSettings } from './PushNotificationSettings'
import * as webPush from './webPush'

const displayMode = vi.hoisted(() => ({ isIos: false, isIpad: false, isStandalone: false }))
vi.mock('./pwaDisplayMode', () => ({ usePwaDisplayMode: () => displayMode }))
vi.mock('./webPush', async importOriginal => {
  const actual = await importOriginal<typeof import('./webPush')>()
  return { ...actual, getWebPushConfig: vi.fn(), supportsWebPush: vi.fn(),
    currentPushSubscription: vi.fn(), activateWebPush: vi.fn(), deactivateWebPush: vi.fn() }
})

function notification(permission: NotificationPermission = 'default') {
  const requestPermission = vi.fn().mockResolvedValue('granted')
  Object.defineProperty(window, 'Notification', { configurable: true, value: { permission, requestPermission } })
  return requestPermission
}

function renderSettings() {
  return render(<ThemeProvider theme={createAppTheme('light')}>
    <PushNotificationSettings open onClose={vi.fn()} />
  </ThemeProvider>)
}

describe('PushNotificationSettings', () => {
  beforeEach(() => {
    displayMode.isIos = false; displayMode.isIpad = false; displayMode.isStandalone = false
    notification()
    vi.mocked(webPush.getWebPushConfig).mockResolvedValue({ enabled: true, vapidPublicKey: 'key' })
    vi.mocked(webPush.supportsWebPush).mockReturnValue(true)
    vi.mocked(webPush.currentPushSubscription).mockResolvedValue(null)
  })

  it('never requests permission automatically and activates only after click', async () => {
    const requestPermission = notification()
    const subscription = { endpoint: 'https://push.test/a' } as PushSubscription
    vi.mocked(webPush.activateWebPush).mockResolvedValue(subscription)
    const view = renderSettings()
    expect(await screen.findByText(/Ative quando quiser/)).toBeInTheDocument()
    view.rerender(<ThemeProvider theme={createAppTheme('light')}>
      <PushNotificationSettings open onClose={vi.fn()} />
    </ThemeProvider>)
    expect(requestPermission).not.toHaveBeenCalled()
    fireEvent.click(screen.getByRole('button', { name: 'Ativar notificações' }))
    await waitFor(() => expect(webPush.activateWebPush).toHaveBeenCalledTimes(1))
    expect(await screen.findByText('Notificações ativadas neste dispositivo.')).toBeInTheDocument()
  })

  it('shows server disabled, denied and unsupported states', async () => {
    vi.mocked(webPush.getWebPushConfig).mockResolvedValueOnce({ enabled: false, vapidPublicKey: null })
    const disabled = renderSettings()
    expect(await screen.findByText('Notificações estão desativadas no servidor.')).toBeInTheDocument()
    disabled.unmount()
    notification('denied')
    const denied = renderSettings()
    expect(await screen.findByText(/bloqueadas neste navegador/)).toBeInTheDocument()
    denied.unmount()
    notification()
    vi.mocked(webPush.supportsWebPush).mockReturnValue(false)
    renderSettings()
    expect(await screen.findByText(/não oferece notificações Web Push/)).toBeInTheDocument()
  })

  it('guides iOS browser but allows an installed Apple PWA', async () => {
    const requestPermission = notification()
    displayMode.isIos = true
    const browser = renderSettings()
    expect(await screen.findByText('Instale o Comvy no seu iPhone')).toBeInTheDocument()
    expect(screen.getByText(/No Safari, toque em •••/)).toBeInTheDocument()
    expect(screen.getByText(/Compartilhar/)).toBeInTheDocument()
    expect(screen.getByText(/Ver Mais/)).toBeInTheDocument()
    expect(screen.getByText(/Adicionar à Tela de Início/)).toBeInTheDocument()
    expect(requestPermission).not.toHaveBeenCalled()
    browser.unmount()
    displayMode.isStandalone = true
    renderSettings()
    expect(await screen.findByRole('button', { name: 'Ativar notificações' })).toBeInTheDocument()
  })

  it('uses the iPad title for iPadOS browser guidance', async () => {
    displayMode.isIos = true
    displayMode.isIpad = true
    renderSettings()
    expect(await screen.findByText('Instale o Comvy no seu iPad')).toBeInTheDocument()
  })

  it('shows and disables only the current device subscription', async () => {
    notification('granted')
    const subscription = { endpoint: 'https://push.test/a' } as PushSubscription
    vi.mocked(webPush.currentPushSubscription).mockResolvedValue(subscription)
    vi.mocked(webPush.deactivateWebPush).mockResolvedValue()
    renderSettings()
    fireEvent.click(await screen.findByRole('button', { name: /Desativar neste dispositivo/ }))
    await waitFor(() => expect(webPush.deactivateWebPush).toHaveBeenCalledWith(subscription))
    expect(await screen.findByRole('button', { name: 'Ativar notificações' })).toBeInTheDocument()
  })
})
