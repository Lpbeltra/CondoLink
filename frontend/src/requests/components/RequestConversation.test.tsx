import { render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { RequestConversation } from './RequestConversation'

vi.mock('../api', () => ({ createRequestMessage: vi.fn() }))

describe('RequestConversation', () => {
  it('shows manager and resident context newest first without duplicate text', () => {
    render(<RequestConversation requestId="request-id" status="InProgress" readOnly
      onMessageCreated={vi.fn()} messages={[
        { id: 'resident', requestId: 'request-id', author: { id: 'resident-id', fullName: 'Maria', isManager: false }, content: 'Relato do morador', channel: 'WhatsAppResidentUpdate', createdAt: '2026-08-01T10:00:00Z' },
        { id: 'manager', requestId: 'request-id', author: { id: 'manager-id', fullName: 'Gestor', isManager: true }, content: 'Atualização da administração', channel: 'Portal', createdAt: '2026-08-02T10:00:00Z' },
        { id: 'duplicate', requestId: 'request-id', author: { id: 'manager-id', fullName: 'Gestor', isManager: true }, content: 'Atualização da administração', channel: 'Portal', createdAt: '2026-07-31T10:00:00Z' },
      ]} />)

    expect(screen.getByText('Relato do morador')).toBeVisible()
    expect(screen.getAllByText('Atualização da administração')).toHaveLength(1)
    const updates = screen.getAllByText(/Atualização da administração|Relato do morador/)
    expect(updates[0]).toHaveTextContent('Atualização da administração')
  })

  it('prefers the current AI summary for the latest resident message', () => {
    render(<RequestConversation requestId="request-id" status="InProgress" readOnly
      residentSummary="Resumo contextual gerado pela IA."
      onMessageCreated={vi.fn()} messages={[
        { id: 'resident', requestId: 'request-id', author: { id: 'resident-id', fullName: 'Maria', isManager: false }, content: 'Transcrição extensa', channel: 'WhatsAppResidentUpdate', createdAt: '2026-08-01T10:00:00Z' },
      ]} />)

    expect(screen.getByText('Resumo contextual gerado pela IA.')).toBeVisible()
    expect(screen.queryByText('Transcrição extensa')).not.toBeInTheDocument()
  })
})
