import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { RequestManagementActions } from './RequestManagementActions'
import { suggestRequestStatusMessage, updateRequestStatus } from '../api'

vi.mock('../api', () => ({
  suggestRequestStatusMessage: vi.fn(),
  updateRequestStatus: vi.fn(),
  updateRequestPriority: vi.fn(),
}))

const renderActions = () => render(<RequestManagementActions
  requestId="request-1" status="InProgress" priority="Normal"
  onUpdated={vi.fn().mockResolvedValue(undefined)} />)

describe('RequestManagementActions AI preview', () => {
  beforeEach(() => vi.clearAllMocks())

  it('shows original and suggestion and sends only the explicitly selected suggestion', async () => {
    vi.mocked(suggestRequestStatusMessage).mockResolvedValue({ suggestion: 'Mensagem revisada.' })
    vi.mocked(updateRequestStatus).mockResolvedValue({})
    const user = userEvent.setup(); renderActions()

    await user.click(screen.getByRole('button', { name: 'Resolver' }))
    await user.type(screen.getByLabelText('Mensagem ao morador (opcional)'), 'Mensagem original.')
    await user.click(screen.getByRole('button', { name: 'Gerar sugestão com IA' }))

    expect(await screen.findByText('Seu texto')).toBeVisible()
    expect(screen.getAllByText('Mensagem original.')).toHaveLength(2)
    expect(screen.getByDisplayValue('Mensagem revisada.')).toBeVisible()
    expect(updateRequestStatus).not.toHaveBeenCalled()
    await user.click(screen.getByRole('button', { name: 'Enviar sugestão da IA' }))
    expect(updateRequestStatus).toHaveBeenCalledWith('request-1', 'Resolved', 'Mensagem revisada.')
  })

  it('prevents duplicate generation and marks a suggestion stale after editing', async () => {
    let resolveSuggestion!: (value: { suggestion: string }) => void
    vi.mocked(suggestRequestStatusMessage).mockReturnValue(new Promise(resolve => { resolveSuggestion = resolve }))
    const user = userEvent.setup(); renderActions()

    await user.click(screen.getByRole('button', { name: 'Resolver' }))
    const input = screen.getByLabelText('Mensagem ao morador (opcional)')
    await user.type(input, 'Texto com acentos.\nSegunda linha.')
    const generate = screen.getByRole('button', { name: 'Gerar sugestão com IA' })
    await user.click(generate)
    expect(generate).toBeDisabled()
    expect(suggestRequestStatusMessage).toHaveBeenCalledTimes(1)
    resolveSuggestion({ suggestion: 'Texto claro.' })
    expect(await screen.findByDisplayValue('Texto claro.')).toBeVisible()
    await user.type(input, ' Alterado')
    expect(screen.getByText(/versão anterior/)).toBeVisible()
    expect(screen.getByRole('button', { name: 'Enviar sugestão da IA' })).toBeDisabled()
  })
})
