import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { RequestManagementActions } from './RequestManagementActions'
import { MemoryRouter, useLocation } from 'react-router-dom'
import { createAdministrativeRequestUpdate, suggestRequestStatusMessage, updateRequestPriority, updateRequestStatus } from '../api'
import type { RequestPriority, RequestStatus } from '../types'

vi.mock('../api', () => ({
  suggestRequestStatusMessage: vi.fn(),
  updateRequestStatus: vi.fn(),
  updateRequestPriority: vi.fn(),
  createAdministrativeRequestUpdate: vi.fn(),
}))

const renderActions = ({
  status = 'InProgress',
  priority = 'Normal',
  onUpdated = vi.fn().mockResolvedValue(undefined),
}: {
  status?: RequestStatus
  priority?: RequestPriority
  onUpdated?: ReturnType<typeof vi.fn>
} = {}) => {
  const view = render(<MemoryRouter><RequestManagementActions
    requestId="request-1" status={status} priority={priority}
    onUpdated={onUpdated} /></MemoryRouter>)
  return { ...view, onUpdated }
}

function LocationProbe() {
  const location = useLocation()
  return <output data-testid="location">{location.pathname}{location.search}</output>
}

describe('RequestManagementActions AI preview', () => {
  beforeEach(() => vi.clearAllMocks())

  it('shows original and suggestion and sends only the explicitly selected suggestion', async () => {
    vi.mocked(suggestRequestStatusMessage).mockResolvedValue({ suggestion: 'Mensagem revisada.' })
    vi.mocked(updateRequestStatus).mockResolvedValue({})
    const user = userEvent.setup(); renderActions()

    await user.click(screen.getByRole('button', { name: 'Resolver' }))
    await act(async () => {
      fireEvent.change(screen.getByLabelText('Mensagem ao morador (opcional)'), { target: { value: 'Mensagem original.' } })
    })
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
    await act(async () => { resolveSuggestion({ suggestion: 'Texto claro.' }) })
    expect(await screen.findByDisplayValue('Texto claro.')).toBeVisible()
    await user.type(input, ' Alterado')
    expect(screen.getByText(/versão anterior/)).toBeVisible()
    expect(screen.getByRole('button', { name: 'Enviar sugestão da IA' })).toBeDisabled()
  })

  it('sends an administrative update while explicitly preserving the status', async () => {
    vi.mocked(createAdministrativeRequestUpdate).mockResolvedValue({})
    const user = userEvent.setup(); renderActions()
    await user.click(screen.getByRole('button', { name: 'Atualizar / enviar mensagem' }))
    expect(screen.getByText(/Status atual:/)).toHaveTextContent('Em andamento')
    await user.type(screen.getByLabelText('Mensagem ao morador'), 'Visita amanhã às 14h.')
    await user.click(screen.getByRole('button', { name: 'Enviar meu texto' }))
    expect(createAdministrativeRequestUpdate).toHaveBeenCalledWith('request-1', 'Visita amanhã às 14h.')
    expect(updateRequestStatus).not.toHaveBeenCalled()
  })

  it('blocks an administrative message above 3000 characters', async () => {
    const user = userEvent.setup(); renderActions()
    await user.click(screen.getByRole('button', { name: 'Atualizar / enviar mensagem' }))
    const input = screen.getByLabelText('Mensagem ao morador')
    fireEvent.change(input, { target: { value: 'x'.repeat(3001) } })
    expect(screen.getByRole('button', { name: 'Enviar meu texto' })).toBeDisabled()
    expect(createAdministrativeRequestUpdate).not.toHaveBeenCalled()
  })

  it('changes status and priority through their public controls', async () => {
    vi.mocked(updateRequestStatus).mockResolvedValue({})
    vi.mocked(updateRequestPriority).mockResolvedValue({} as never)
    const user = userEvent.setup()
    const { onUpdated } = renderActions()

    await user.click(screen.getByRole('button', { name: 'Alterar status' }))
    await user.click(screen.getByRole('combobox'))
    await user.click(await screen.findByRole('option', { name: 'Aguardando morador' }))
    await user.click(screen.getByRole('button', { name: 'Confirmar' }))
    expect(updateRequestStatus).toHaveBeenCalledWith('request-1', 'WaitingForResident', null)
    expect(onUpdated).toHaveBeenCalledTimes(1)
    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'Alterar status' })).not.toBeInTheDocument())

    await user.click(screen.getByRole('button', { name: 'Alterar prioridade' }))
    await user.click(screen.getByRole('combobox'))
    await user.click(await screen.findByRole('option', { name: 'Urgente' }))
    await user.click(screen.getByRole('button', { name: 'Salvar' }))
    expect(updateRequestPriority).toHaveBeenCalledWith('request-1', 'Urgent')
    expect(onUpdated).toHaveBeenCalledTimes(2)
  })

  it('executes cancel and reopen using the exact workflow statuses', async () => {
    vi.mocked(updateRequestStatus).mockResolvedValue({})
    const user = userEvent.setup()
    const active = renderActions()

    await user.click(screen.getByRole('button', { name: 'Cancelar' }))
    await user.click(screen.getByRole('button', { name: 'Confirmar cancelamento' }))
    expect(updateRequestStatus).toHaveBeenCalledWith('request-1', 'Cancelled', null)
    active.unmount()

    renderActions({ status: 'Cancelled' })
    await user.click(screen.getByRole('button', { name: 'Reabrir solicitação' }))
    await user.click(screen.getByRole('button', { name: 'Confirmar reabertura' }))
    expect(updateRequestStatus).toHaveBeenLastCalledWith('request-1', 'Open', null)
  })

  it('exposes only actions valid for the current status', () => {
    const active = renderActions({ status: 'WaitingForResidentClosure' })
    expect(screen.queryByRole('button', { name: 'Resolver' })).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Alterar status' })).toBeVisible()
    expect(screen.getByRole('button', { name: 'Cancelar' })).toBeVisible()
    active.unmount()

    renderActions({ status: 'Resolved' })
    expect(screen.getByRole('button', { name: 'Reabrir solicitação' })).toBeVisible()
    expect(screen.queryByRole('button', { name: 'Alterar prioridade' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Atualizar / enviar mensagem' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Cancelar' })).not.toBeInTheDocument()
  })

  it('opens the linked reminder with its stable identifier', async () => {
    const user = userEvent.setup()
    render(<MemoryRouter><RequestManagementActions
      requestId="request-1" status="InProgress" priority="Normal"
      agendaReminder={{ id: 'reminder-7', title: 'Retorno', nextOccurrenceAtUtc: null,
        recurrenceType: 'None', isActive: true, completedAt: null }}
      onUpdated={vi.fn().mockResolvedValue(undefined)} /><LocationProbe /></MemoryRouter>)

    await user.click(screen.getByRole('button', { name: 'Abrir lembrete' }))
    expect(screen.getByTestId('location')).toHaveTextContent('/management/agenda?reminderId=reminder-7')
  })

  it('prevents duplicate status submission while the first request is pending', async () => {
    let finish!: (value: unknown) => void
    vi.mocked(updateRequestStatus).mockReturnValue(new Promise(resolve => { finish = resolve }) as never)
    const user = userEvent.setup()
    renderActions()

    await user.click(screen.getByRole('button', { name: 'Resolver' }))
    const submit = screen.getByRole('button', { name: 'Enviar conclusão' })
    await user.click(submit)
    expect(submit).toBeDisabled()
    fireEvent.click(submit)
    expect(updateRequestStatus).toHaveBeenCalledTimes(1)

    await act(async () => { finish({}) })
  })

  it('surfaces a conflict and does not report a successful update', async () => {
    vi.mocked(updateRequestStatus).mockRejectedValue({ isAxiosError: true, response: { status: 409 } })
    const user = userEvent.setup()
    const { onUpdated } = renderActions()

    await user.click(screen.getByRole('button', { name: 'Cancelar' }))
    await user.click(screen.getByRole('button', { name: 'Confirmar cancelamento' }))

    expect(await screen.findByText('Esta alteração não é mais válida. Atualize os dados e tente novamente.')).toBeVisible()
    expect(onUpdated).not.toHaveBeenCalled()
  })
})
