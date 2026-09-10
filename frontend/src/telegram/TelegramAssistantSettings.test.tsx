import { ThemeProvider } from '@mui/material'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createAppTheme } from '../theme/createAppTheme'
import { TelegramAssistantSettings } from './TelegramAssistantSettings'
import * as telegram from './api'

vi.mock('./api', () => ({ getTelegramStatus: vi.fn(), createTelegramLinkCode: vi.fn(), unlinkTelegram: vi.fn() }))
function view() { return render(<ThemeProvider theme={createAppTheme('light')}><TelegramAssistantSettings open onClose={vi.fn()} /></ThemeProvider>) }
describe('TelegramAssistantSettings', () => {
  beforeEach(() => { vi.clearAllMocks(); vi.mocked(telegram.getTelegramStatus).mockResolvedValue({ enabled: true,
    botUsername: 'comvy_bot', linked: false, linkedAt: null, activeCondominiumName: null }) })
  it('generates a temporary code and deep link', async () => {
    vi.mocked(telegram.createTelegramLinkCode).mockResolvedValue({ code: 'ABC234XY',
      deepLink: 'https://t.me/comvy_bot?start=ABC234XY', expiresAt: new Date().toISOString() })
    view(); fireEvent.click(await screen.findByRole('button', { name: 'Vincular Telegram' }))
    expect(await screen.findByText(/\/start ABC234XY/)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Abrir bot no Telegram' })).toHaveAttribute('href', expect.stringContaining('start=ABC234XY'))
  })
  it('shows linked state and unlinks', async () => {
    vi.mocked(telegram.getTelegramStatus).mockResolvedValue({ enabled: true, botUsername: 'comvy_bot', linked: true,
      linkedAt: '2026-09-10T12:00:00Z', activeCondominiumName: 'Monticello' })
    view(); expect(await screen.findByText('Telegram conectado')).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Desvincular' }))
    await waitFor(() => expect(telegram.unlinkTelegram).toHaveBeenCalledOnce())
  })
  it('handles disabled server', async () => {
    vi.mocked(telegram.getTelegramStatus).mockResolvedValue({ enabled: false, botUsername: null, linked: false,
      linkedAt: null, activeCondominiumName: null })
    view(); expect(await screen.findByText(/está desativado/)).toBeInTheDocument()
  })
})
