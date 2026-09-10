import { ThemeProvider } from '@mui/material'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createAppTheme } from '../theme/createAppTheme'
import { PwaUpdatePrompt } from './PwaUpdatePrompt'
import {
  notifyPwaUpdateAvailable,
  resetPwaUpdateForTests,
  setPwaUpdateHandler,
} from './pwaUpdate'

function renderPrompt() {
  return render(<ThemeProvider theme={createAppTheme('light')}><PwaUpdatePrompt /></ThemeProvider>)
}

describe('PwaUpdatePrompt', () => {
  beforeEach(() => resetPwaUpdateForTests())

  it('shows an update detected before the UI mounts', async () => {
    notifyPwaUpdateAvailable()
    renderPrompt()
    expect(await screen.findByText('Nova versão do Comvy disponível.')).toBeVisible()
  })

  it('offers one explicit update action and prevents duplicate activation', async () => {
    let finish!: () => void
    const update = vi.fn(() => new Promise<void>(resolve => { finish = resolve }))
    setPwaUpdateHandler(update)
    notifyPwaUpdateAvailable()
    renderPrompt()

    const button = screen.getByRole('button', { name: 'Atualizar agora' })
    fireEvent.click(button)
    fireEvent.click(button)

    expect(update).toHaveBeenCalledOnce()
    expect(update).toHaveBeenCalledWith(true)
    expect(await screen.findByRole('button', { name: 'Atualizando…' })).toBeDisabled()
    finish()
  })

  it('keeps the app usable and allows retry after activation failure', async () => {
    vi.spyOn(console, 'error').mockImplementation(() => undefined)
    const update = vi.fn()
      .mockRejectedValueOnce(new Error('activation failed'))
      .mockResolvedValueOnce(undefined)
    setPwaUpdateHandler(update)
    notifyPwaUpdateAvailable()
    renderPrompt()

    fireEvent.click(screen.getByRole('button', { name: 'Atualizar agora' }))
    expect(await screen.findByText('Não foi possível atualizar o Comvy. Tente novamente.')).toBeVisible()

    fireEvent.click(screen.getByRole('button', { name: 'Atualizar agora' }))
    await waitFor(() => expect(update).toHaveBeenCalledTimes(2))
  })
})
