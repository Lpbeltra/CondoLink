import { ThemeProvider } from '@mui/material'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { createAppTheme } from '../theme/createAppTheme'
import { PwaInstallBanner } from './PwaInstallBanner'

function browser(options: { mobile?: boolean; standalone?: boolean; userAgent?: string } = {}) {
  Object.defineProperty(navigator, 'userAgent', {
    configurable: true,
    value: options.userAgent ?? (options.mobile ? 'Mozilla/5.0 (Linux; Android 14)' : 'Mozilla/5.0 (Windows NT 10.0)'),
  })
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

  it('does not appear on desktop or when already standalone', () => {
    browser()
    const desktop = renderBanner()
    fireEvent(window, installEvent())
    expect(screen.queryByRole('region', { name: 'Instalar Comvy' })).not.toBeInTheDocument()
    desktop.unmount()
    browser({ mobile: true, standalone: true })
    renderBanner()
    fireEvent(window, installEvent())
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

  it('shows concise iOS instructions and explains when Safari is needed', async () => {
    browser({ mobile: true, userAgent: 'Mozilla/5.0 (iPhone) CriOS/125.0 Mobile' })
    renderBanner()
    await userEvent.click(screen.getByRole('button', { name: 'Instalar Comvy' }))
    expect(screen.getByText('Abra esta página no Safari.')).toBeVisible()
    expect(screen.getByText(/Adicionar à Tela de Início/)).toBeVisible()
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
