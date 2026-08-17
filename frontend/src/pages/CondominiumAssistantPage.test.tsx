import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { CondominiumAssistantPage } from './CondominiumAssistantPage'

const assistant = vi.hoisted(() => ({
  listConversations: vi.fn(), getConversation: vi.fn(), startConversation: vi.fn(),
  askAssistant: vi.fn(), removeRequestContext: vi.fn(), deleteConversation: vi.fn(),
}))
vi.mock('../assistant/api', async importOriginal => ({ ...(await importOriginal<typeof import('../assistant/api')>()), ...assistant }))
vi.mock('../management/ManagementContext', () => ({ useManagementContext: () => ({ activeCondominiumId: 'condo-1' }) }))

describe('CondominiumAssistantPage', () => {
  beforeEach(() => {
    Object.values(assistant).forEach(mock => mock.mockReset())
    assistant.listConversations.mockResolvedValue({ items: [], hasMore: false, total: 0 })
  })

  it('shows empty history and does not persist an empty new conversation', async () => {
    render(<MemoryRouter><CondominiumAssistantPage /></MemoryRouter>)
    expect(await screen.findByText('Nenhuma conversa anterior.')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Nova conversa' }))
    expect(assistant.startConversation).not.toHaveBeenCalled()
    expect(screen.getByText('Quais são as regras da piscina?')).toBeInTheDocument()
  })

  it('reopens persisted messages with historical sources', async () => {
    assistant.listConversations.mockResolvedValue({ items: [{ id: 'chat-1', title: 'Barulho', requestId: 'request-1', requestTitle: 'Barulho na unidade', createdAt: '2026-08-17T10:00:00Z', updatedAt: '2026-08-17T11:00:00Z' }], hasMore: false, total: 1 })
    assistant.getConversation.mockResolvedValue({ conversation: { id: 'chat-1', title: 'Barulho', requestId: 'request-1' }, requestContext: { id: 'request-1', title: 'Barulho na unidade' }, contextUnavailable: false, messages: [{ id: 'm1', role: 'Assistant', content: 'Resposta anterior', createdAt: '2026-08-17T11:00:00Z', sources: [{ source: { documentId: 'doc-1', documentName: 'Regimento', pageNumber: 12, sectionTitle: null, excerpt: '...', marker: 'S1' }, documentCurrentlyActive: false }] }] })
    render(<MemoryRouter><CondominiumAssistantPage /></MemoryRouter>)
    await userEvent.click(await screen.findByRole('button', { name: /Barulho/ }))
    expect(await screen.findByText('Resposta anterior')).toBeInTheDocument()
    expect(screen.getByText(/documento atualmente inativo/)).toBeInTheDocument()
    await waitFor(() => expect(assistant.getConversation).toHaveBeenCalledWith('condo-1', 'chat-1'))
  })
})
