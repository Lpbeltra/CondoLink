import { render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { RequestConversation } from './RequestConversation'

vi.mock('../api', () => ({ createRequestMessage: vi.fn() }))

describe('RequestConversation', () => {
  it('does not duplicate resident reports in the general updates timeline', () => {
    Element.prototype.scrollIntoView = vi.fn()
    render(<RequestConversation requestId="request-id" status="InProgress" readOnly
      onMessageCreated={vi.fn()} messages={[
        { id: 'resident', requestId: 'request-id', author: { id: 'resident-id', fullName: 'Maria', isManager: false }, content: 'Relato do morador', channel: 'WhatsAppResidentUpdate', createdAt: '2026-08-01T10:00:00Z' },
        { id: 'manager', requestId: 'request-id', author: { id: 'manager-id', fullName: 'Gestor', isManager: true }, content: 'Atualização da administração', channel: 'Portal', createdAt: '2026-08-02T10:00:00Z' },
      ]} />)

    expect(screen.queryByText('Relato do morador')).not.toBeInTheDocument()
    expect(screen.getByText('Atualização da administração')).toBeVisible()
  })
})
