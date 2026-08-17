import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { RequestTimeline } from './RequestTimeline'

describe('RequestTimeline', () => {
  it('shows spontaneous resident content beside its contextual event', () => {
    render(<RequestTimeline history={[]} messages={[{
      id: 'message', requestId: 'request',
      author: { id: 'resident', fullName: 'Maria', isManager: false },
      content: 'O porteiro informou que a TAG já chegou.',
      channel: 'WhatsAppResidentUpdate', createdAt: '2026-08-10T17:30:00Z',
    }]} />)

    expect(screen.getByText('Atualização do morador')).toBeVisible()
    expect(screen.getByText('O porteiro informou que a TAG já chegou.')).toBeVisible()
  })

  it('correlates the requested reply with the automatic status event', () => {
    render(<RequestTimeline history={[{
      id: 'history', previousStatus: 'WaitingForResident', newStatus: 'InProgress',
      changedByUserId: 'resident', changedByFullName: 'Maria', reason: 'Resposta recebida do morador.',
      createdAt: '2026-08-10T17:30:00Z', answerMessageId: 'answer',
    }]} messages={[{
      id: 'answer', requestId: 'request',
      author: { id: 'resident', fullName: 'Maria', isManager: false },
      content: 'O portão voltou a travar.', channel: 'WhatsAppResidentUpdate',
      createdAt: '2026-08-10T17:30:00Z', isResidentReply: true,
    }]} />)

    expect(screen.getByText('Status alterado para Em andamento')).toBeVisible()
    expect(screen.getByText('Resposta recebida do morador: O portão voltou a travar.')).toBeVisible()
    expect(screen.queryByText('Atualização do morador')).not.toBeInTheDocument()
  })

  it('distinguishes proposed and automatic closure events', () => {
    render(<RequestTimeline history={[
      { id: 'proposal', previousStatus: 'InProgress', newStatus: 'WaitingForResidentClosure', changedByUserId: 'manager', changedByFullName: 'Ana', reason: 'Tag entregue.', createdAt: '2026-08-17T11:35:00Z' },
      { id: 'automatic', previousStatus: 'WaitingForResidentClosure', newStatus: 'Resolved', changedByUserId: 'manager', changedByFullName: 'Ana', reason: 'O prazo para manifestação foi encerrado.', createdAt: '2026-08-17T12:35:00Z' },
    ]} />)
    expect(screen.getByText('Concluído pela administração — aguardando confirmação')).toBeVisible()
    expect(screen.getByText('Atendimento finalizado automaticamente')).toBeVisible()
  })
})
