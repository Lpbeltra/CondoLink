import { ThemeProvider } from '@mui/material'
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { createAppTheme } from '../theme/createAppTheme'
import { PwaUpdatePrompt } from './PwaUpdatePrompt'
import { notifyPwaUpdateAvailable, setPwaUpdateHandler } from './pwaUpdate'

describe('PwaUpdatePrompt', () => {
  it('offers one explicit update action and delegates activation/reload', async () => {
    const update = vi.fn().mockResolvedValue(undefined)
    setPwaUpdateHandler(update)
    render(<ThemeProvider theme={createAppTheme('light')}><PwaUpdatePrompt /></ThemeProvider>)
    act(() => notifyPwaUpdateAvailable())
    expect(await screen.findByText('Nova versão do Comvy disponível.')).toBeVisible()
    fireEvent.click(screen.getByRole('button', { name: 'Atualizar agora' }))
    await waitFor(() => expect(update).toHaveBeenCalledWith(true))
  })
})
