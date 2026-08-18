import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'

const actions = vi.hoisted(() => ({ confirmResidentClosure: vi.fn(), questionResidentClosure: vi.fn() }))
vi.mock('../api', () => actions)
import { ResidentClosurePanel } from './ResidentClosurePanel'

const proposal = { conclusion: 'O reparo foi concluído na portaria.', requestedAt: '2026-08-18T12:00:00Z', expiresAt: '2026-08-18T13:00:00Z' }

describe('ResidentClosurePanel', () => {
  beforeEach(() => { actions.confirmResidentClosure.mockReset(); actions.questionResidentClosure.mockReset() })

  it('shows the proposal and confirms through the residential action', async () => {
    actions.confirmResidentClosure.mockResolvedValue({ code: 'confirmed' })
    const onUpdated = vi.fn()
    render(<ResidentClosurePanel requestId="request-id" proposal={proposal} onUpdated={onUpdated} />)
    expect(screen.getByText(proposal.conclusion)).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Concordar e finalizar' }))
    expect(actions.confirmResidentClosure).toHaveBeenCalledWith('request-id')
    expect(await screen.findByText('Atendimento finalizado. Obrigado pela confirmação.')).toBeInTheDocument()
    expect(onUpdated).toHaveBeenCalled()
  })

  it('only questions after a message is submitted', async () => {
    actions.questionResidentClosure.mockResolvedValue({ code: 'questioned' })
    render(<ResidentClosurePanel requestId="request-id" proposal={proposal} onUpdated={vi.fn()} />)
    await userEvent.click(screen.getByRole('button', { name: 'Ainda tenho uma dúvida' }))
    expect(actions.questionResidentClosure).not.toHaveBeenCalled()
    await userEvent.type(screen.getByLabelText('Escreva sua dúvida ou observação'), 'Ainda não funciona.')
    await userEvent.click(screen.getByRole('button', { name: 'Enviar dúvida' }))
    expect(actions.questionResidentClosure).toHaveBeenCalledWith('request-id', 'Ainda não funciona.')
    expect(await screen.findByText(/voltou para análise da administração/)).toBeInTheDocument()
  })

  it('refreshes stale state after a competing channel wins', async () => {
    actions.confirmResidentClosure.mockRejectedValue({ isAxiosError: true, response: { status: 409 } })
    const onUpdated = vi.fn()
    render(<ResidentClosurePanel requestId="request-id" proposal={proposal} onUpdated={onUpdated} />)
    await userEvent.click(screen.getByRole('button', { name: 'Concordar e finalizar' }))
    expect(await screen.findByText('Este atendimento já foi atualizado.')).toBeInTheDocument()
    expect(onUpdated).toHaveBeenCalled()
  })
})
