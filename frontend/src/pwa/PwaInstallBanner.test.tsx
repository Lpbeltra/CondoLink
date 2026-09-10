import { ThemeProvider } from '@mui/material'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { createAppTheme } from '../theme/createAppTheme'
import { PwaInstallBanner } from './PwaInstallBanner'

function browser(options: {
  mobile?: boolean
  standalone?: boolean
  iosStandalone?: boolean
  userAgent?: string
  platform?: string
  maxTouchPoints?: number
} = {}) {
  Object.defineProperty(navigator, 'userAgent', {
    configurable: true,
    value: options.userAgent ?? (options.mobile ? 'Mozilla/5.0 (Linux; Android 14)' : 'Mozilla/5.0 (Windows NT 10.0)'),
  })
  Object.defineProperty(navigator, 'platform', { configurable: true, value: options.platform ?? '' })
  Object.defineProperty(navigator, 'maxTouchPoints', { configurable: true, value: options.maxTouchPoints ?? 0 })
  Object.defineProperty(navigator, 'standalone', { configurable: true, value: Boolean(options.iosStandalone) })
  Object.defineProperty(window, 'matchMedia', {
    configurable: true,
    value: (query: string) => ({
      matches: query.includes('display-mode') ? Boolean(options.standalone) : Boolean(options.mobile),
      media: query, onchange: null,
      addEventListener() {}, removeEventListener() {}, addListener() {}, removeListener() {},
      dispatchEvent: () => false,
    }),
  })
}

function installEvent(outcome: 'accepted' | 'dismissed' = 'accepted') {
  const event = new Event('beforeinstallprompt', { cancelable: true }) as Event & {
    prompt: ReturnType<typeof vi.fn>
    userChoice: Promise<{ outcome: 'accepted' | 'dismissed'; platform: string }>
  }
  event.prompt = vi.fn().mockResolvedValue(undefined)
  event.userChoice = Promise.resolve({ outcome, platform: 'web' })
  return event
}

function renderBanner(mode: 'light' | 'dark' = 'light') {
  return render(<ThemeProvider theme={createAppTheme(mode)}><PwaInstallBanner /></ThemeProvider>)
}

describe('PwaInstallBanner', () => {
  it.each(['light', 'dark'] as const)('appears in eligible Android mobile context in %s mode', mode => {
    browser({ mobile: true })
    renderBanner(mode)
    fireEvent(window, installEvent())
    expect(screen.getByRole('region', { name: 'Instalar Comvy' })).toBeVisible()
  })

  it('uses native installation on desktop without showing Apple instructions', async () => {
    browser()
    const event = installEvent()
    renderBanner()
    fireEvent(window, event)
    expect(screen.getByRole('region', { name: 'Instalar Comvy' })).toBeVisible()
    expect(screen.getByText('Instale o Comvy neste computador')).toBeVisible()
    expect(screen.queryByText(/Compartilhar/)).not.toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Instalar Comvy' }))
    expect(event.prompt).toHaveBeenCalledOnce()
  })

  it('appears on a compatible Android tablet regardless of viewport width', () => {
    browser({ userAgent: 'Mozilla/5.0 (Linux; Android 14; Pixel Tablet)' })
    renderBanner()
    fireEvent(window, installEvent())
    expect(screen.getByRole('region', { name: 'Instalar Comvy' })).toBeVisible()
    expect(screen.getByText('Instale o Comvy')).toBeVisible()
    expect(screen.queryByText(/Compartilhar/)).not.toBeInTheDocument()
  })

  it('does not appear when Chromium or iOS is already standalone', () => {
    browser({ mobile: true, standalone: true })
    const chromium = renderBanner()
    fireEvent(window, installEvent())
    expect(screen.queryByRole('region', { name: 'Instalar Comvy' })).not.toBeInTheDocument()
    chromium.unmount()
    browser({ userAgent: 'Mozilla/5.0 (iPhone)', iosStandalone: true })
    renderBanner()
    expect(screen.queryByRole('region', { name: 'Instalar Comvy' })).not.toBeInTheDocument()
  })

  it('uses the Chromium native install prompt and hides after acceptance', async () => {
    browser({ mobile: true })
    const event = installEvent('accepted')
    renderBanner()
    fireEvent(window, event)
    await userEvent.click(screen.getByRole('button', { name: 'Instalar Comvy' }))
    expect(event.prompt).toHaveBeenCalledOnce()
    await waitFor(() => expect(screen.queryByRole('region', { name: 'Instalar Comvy' })).not.toBeInTheDocument())
  })

  it('shows the complete iPhone installation tutorial without invoking a native prompt', () => {
    browser({ mobile: true, userAgent: 'Mozilla/5.0 (iPhone) CriOS/125.0 Mobile' })
    renderBanner()
    const event = installEvent()
    fireEvent(window, event)
    expect(screen.getByText('Instale o Comvy no seu iPhone')).toBeVisible()
    expect(screen.getByText(/No Safari, toque em •••/)).toBeVisible()
    expect(screen.getByText(/Toque em Compartilhar/)).toBeVisible()
    expect(screen.getByText(/Se necessário, toque em Ver Mais/)).toBeVisible()
    expect(screen.getByText(/Escolha Adicionar à Tela de Início/)).toBeVisible()
    expect(event.prompt).not.toHaveBeenCalled()
    expect(screen.queryByRole('button', { name: 'Instalar Comvy' })).not.toBeInTheDocument()
  })

  it('detects modern iPadOS using desktop user agent', () => {
    browser({ userAgent: 'Mozilla/5.0 (Macintosh; Intel Mac OS X) AppleWebKit/605.1.15 Safari/605.1.15', platform: 'MacIntel', maxTouchPoints: 5 })
    renderBanner()
    expect(screen.getByRole('region', { name: 'Instalar Comvy' })).toBeVisible()
    expect(screen.getByText('Instale o Comvy no seu iPad')).toBeVisible()
    expect(screen.getByText(/Adicionar à Tela de Início/)).toBeVisible()
  })

  it('hides an offered prompt after appinstalled', () => {
    browser({ mobile: true })
    renderBanner()
    fireEvent(window, installEvent())
    expect(screen.getByRole('region', { name: 'Instalar Comvy' })).toBeVisible()
    fireEvent(window, new Event('appinstalled'))
    expect(screen.queryByRole('region', { name: 'Instalar Comvy' })).not.toBeInTheDocument()
  })

  it('persists Agora não and does not immediately reappear', async () => {
    browser({ mobile: true })
    const first = renderBanner()
    fireEvent(window, installEvent())
    await userEvent.click(screen.getByRole('button', { name: 'Agora não' }))
    first.unmount()
    renderBanner()
    fireEvent(window, installEvent())
    expect(screen.queryByRole('region', { name: 'Instalar Comvy' })).not.toBeInTheDocument()
  })
})
