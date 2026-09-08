import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { RequestConversation } from './RequestConversation'
import { createRequestMessage } from '../api'

vi.mock('../api', () => ({ createRequestMessage: vi.fn() }))

describe('RequestConversation', () => {
  it('shows original communication chronologically, preserving repeated messages', () => {
    render(<RequestConversation requestId="request-id" status="InProgress" readOnly
      onMessageCreated={vi.fn()} messages={[
        { id: 'resident', requestId: 'request-id', author: { id: 'resident-id', fullName: 'Maria', isManager: false }, content: 'Relato do morador', channel: 'WhatsAppResidentUpdate', createdAt: '2026-08-01T10:00:00Z' },
        { id: 'manager', requestId: 'request-id', author: { id: 'manager-id', fullName: 'Gestor', isManager: true }, content: 'Visita confirmada para amanhã.', channel: 'Portal', createdAt: '2026-08-02T10:00:00Z' },
        { id: 'duplicate', requestId: 'request-id', author: { id: 'manager-id', fullName: 'Gestor', isManager: true }, content: 'Visita confirmada para amanhã.', channel: 'Portal', createdAt: '2026-07-31T10:00:00Z' },
      ]} />)

    expect(screen.getByText('Relato do morador')).toBeVisible()
    expect(screen.getAllByRole('article', { name: 'Mensagem da gestão' })).toHaveLength(2)
    expect(screen.getAllByText('Visita confirmada para amanhã.')).toHaveLength(2)
    const messages = screen.getAllByRole('article')
    expect(within(messages[0]).getByText('Gestão · Gestor')).toBeVisible()
    expect(within(messages[1]).getByText('Morador · Maria')).toBeVisible()
  })

  it('keeps the resident message as sent without replacing it with an AI summary', () => {
    render(<RequestConversation requestId="request-id" status="InProgress" readOnly
      onMessageCreated={vi.fn()} messages={[
        { id: 'resident', requestId: 'request-id', author: { id: 'resident-id', fullName: 'Maria', isManager: false }, content: 'Transcrição extensa', channel: 'WhatsAppResidentUpdate', createdAt: '2026-08-01T10:00:00Z' },
      ]} />)

    expect(screen.getByText('Transcrição extensa')).toBeVisible()
  })

  it('shows a requested resident reply in updates once and keeps the contextual label', () => {
    render(<RequestConversation requestId="request-id" status="InProgress" readOnly
      onMessageCreated={vi.fn()} messages={[
        { id: 'original', requestId: 'request-id', author: { id: 'resident-id', fullName: 'Maria', isManager: false }, content: 'Relato original', channel: 'WhatsApp', createdAt: '2026-08-01T09:00:00Z' },
        { id: 'reply', requestId: 'request-id', author: { id: 'resident-id', fullName: 'Maria', isManager: false }, content: 'O portão voltou a travar.', channel: 'Portal', createdAt: '2026-08-01T10:00:00Z', isResidentReply: true },
      ]} />)

    expect(screen.getAllByText('Morador · Maria')).toHaveLength(2)
    expect(screen.getAllByText('O portão voltou a travar.')).toHaveLength(1)
    expect(screen.getByText('Relato original')).toBeVisible()
  })

  it('excludes system events even when authored by management', () => {
    render(<RequestConversation requestId="request" status="InProgress" readOnly onMessageCreated={vi.fn()}
      messages={[{ id: 'system', requestId: 'request', author: { id: 'manager', fullName: 'Ana', isManager: true },
        content: 'Fechamento automático', channel: 'Portal', createdAt: '2026-08-01T10:00:00Z', isAdministrativeEvent: true }]} />)
    expect(screen.queryByText('Fechamento automático')).not.toBeInTheDocument()
    expect(screen.queryByRole('article')).not.toBeInTheDocument()
  })

  it('sends communication through the existing message endpoint', async () => {
    const onCreated = vi.fn()
    const message = { id: 'message', requestId: 'request', author: { id: 'manager', fullName: 'Ana', isManager: true },
      content: 'Retorno amanhã.', createdAt: '2026-08-01T10:00:00Z' }
    vi.mocked(createRequestMessage).mockResolvedValue(message)
    render(<RequestConversation requestId="request" status="InProgress" messages={[]} onMessageCreated={onCreated} />)
    const user = userEvent.setup()
    await user.type(screen.getByRole('textbox'), message.content)
    await user.click(screen.getByRole('button', { name: 'Adicionar atualização' }))
    expect(createRequestMessage).toHaveBeenCalledWith('request', message.content)
    expect(onCreated).toHaveBeenCalledWith(message)
    expect(screen.getByRole('textbox')).toHaveValue('')
  })
})
