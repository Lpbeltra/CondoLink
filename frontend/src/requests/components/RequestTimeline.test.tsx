import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { RequestTimeline } from './RequestTimeline'

describe('RequestTimeline', () => {
  it('shows resident content beside its event', () => {
    render(<RequestTimeline history={[]} messages={[{ id: 'message', requestId: 'request', author: { id: 'resident', fullName: 'Maria', isManager: false }, content: 'resident update', channel: 'WhatsAppResidentUpdate', createdAt: '2026-08-10T17:30:00Z' }]} />)
    expect(screen.getByText(/Atualiza/)).toBeVisible()
    expect(screen.getByText('resident update')).toBeVisible()
  })

  it('correlates resident reply with status event', () => {
    render(<RequestTimeline history={[{ id: 'history', previousStatus: 'WaitingForResident', newStatus: 'InProgress', changedByUserId: 'resident', changedByFullName: 'Maria', reason: 'reply received', createdAt: '2026-08-10T17:30:00Z', answerMessageId: 'answer' }]} messages={[{ id: 'answer', requestId: 'request', author: { id: 'resident', fullName: 'Maria', isManager: false }, content: 'gate fixed', channel: 'WhatsAppResidentUpdate', createdAt: '2026-08-10T17:30:00Z', isResidentReply: true }]} />)
    expect(screen.getByText(/Status alterado/)).toBeVisible()
  })

  it('distinguishes closure events', () => {
    render(<RequestTimeline history={[{ id: 'proposal', previousStatus: 'InProgress', newStatus: 'WaitingForResidentClosure', changedByUserId: 'manager', changedByFullName: 'Ana', reason: 'delivered', createdAt: '2026-08-17T11:35:00Z' }, { id: 'automatic', previousStatus: 'WaitingForResidentClosure', newStatus: 'Resolved', changedByUserId: 'manager', changedByFullName: 'Ana', reason: 'expired', createdAt: '2026-08-17T12:35:00Z' }]} />)
    expect(screen.getByText(/aguardando confirma/)).toBeVisible()
  })

  it('shows provider history without rewriting old events', () => {
    render(<RequestTimeline history={[]} serviceProviderHistory={[{ id: 'linked', eventType: 'Linked', providerName: 'Cesar', providerSpecialty: 'Plumbing', changedByFullName: 'Ana', createdAt: '2026-08-17T11:35:00Z' }, { id: 'removed', eventType: 'Removed', previousName: 'Cesar', previousSpecialty: 'Plumbing', changedByFullName: 'Ana', createdAt: '2026-08-17T12:35:00Z' }]} />)
    expect(screen.getByText('Prestador vinculado')).toBeVisible()
    expect(screen.getAllByText('Cesar — Plumbing')).toHaveLength(2)
    expect(screen.getByText('Prestador removido')).toBeVisible()
  })
})
