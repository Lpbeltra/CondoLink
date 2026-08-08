import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { acknowledgeResidentUpdate } from '../api'
import { ResidentUpdateAcknowledgement } from './ResidentUpdateAcknowledgement'

vi.mock('../api', () => ({ acknowledgeResidentUpdate: vi.fn() }))

describe('ResidentUpdateAcknowledgement', () => {
  it('keeps indicator until explicit acknowledgement and removes only it', async () => {
    vi.mocked(acknowledgeResidentUpdate).mockResolvedValue(undefined)
    const acknowledged = vi.fn()
    const user = userEvent.setup()
    render(<ResidentUpdateAcknowledgement requestId="request-id" visible
      onAcknowledged={acknowledged} />)

    expect(screen.getByText('Atualizado pelo morador')).toBeVisible()
    expect(acknowledgeResidentUpdate).not.toHaveBeenCalled()
    await user.click(screen.getByRole('button', { name: '✓ Marcar como ciente' }))

    await waitFor(() => expect(screen.queryByText('Atualizado pelo morador'))
      .not.toBeInTheDocument())
    expect(acknowledgeResidentUpdate).toHaveBeenCalledWith('request-id')
    expect(acknowledged).toHaveBeenCalledOnce()
  })
})
