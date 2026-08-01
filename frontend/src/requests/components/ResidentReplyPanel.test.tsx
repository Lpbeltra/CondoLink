import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'

const requestApi = vi.hoisted(() => ({ createResidentReply: vi.fn() }))
vi.mock('../api', () => requestApi)
import { ResidentReplyPanel } from './ResidentReplyPanel'

const requirement = { id: 'requirement-id', question: 'Envie uma foto do portão.', requestedAt: '2026-08-01T10:00:00Z', isActive: true }

describe('ResidentReplyPanel', () => {
  beforeEach(() => requestApi.createResidentReply.mockReset())

  it('shows the active question and validates empty content', async () => {
    const user = userEvent.setup(); render(<ResidentReplyPanel requestId="request-id" requirement={requirement} onSent={vi.fn()} />)
    expect(screen.getByText('A administração precisa de uma informação sua')).toBeInTheDocument()
    expect(screen.getByText(requirement.question)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Enviar resposta' })).toBeDisabled()
    await user.type(screen.getByLabelText('Sua resposta'), '   ')
    expect(screen.getByRole('button', { name: 'Enviar resposta' })).toBeDisabled()
  })

  it('selects and removes an attachment before sending', async () => {
    const user = userEvent.setup(); const { container } = render(<ResidentReplyPanel requestId="request-id" requirement={requirement} onSent={vi.fn()} />)
    const input = container.querySelector<HTMLInputElement>('input[type="file"]')!
    await user.upload(input, new File(['x'], 'foto.png', { type: 'image/png' }))
    expect(screen.getByText('foto.png')).toBeInTheDocument()
    await user.click(screen.getByTestId('CloseRoundedIcon'))
    expect(screen.queryByText('foto.png')).not.toBeInTheDocument()
  })

  it('sends text and refreshes the details after success', async () => {
    requestApi.createResidentReply.mockResolvedValue({ messageId: 'message-id', status: 'InProgress' })
    const onSent = vi.fn(); const user = userEvent.setup()
    render(<ResidentReplyPanel requestId="request-id" requirement={requirement} onSent={onSent} />)
    await user.type(screen.getByLabelText('Sua resposta'), 'Segue a informação.')
    await user.click(screen.getByRole('button', { name: 'Enviar resposta' }))
    await waitFor(() => expect(requestApi.createResidentReply).toHaveBeenCalledWith('request-id', 'Segue a informação.', [], expect.any(Function)))
    expect(onSent).toHaveBeenCalledOnce()
  })
})
