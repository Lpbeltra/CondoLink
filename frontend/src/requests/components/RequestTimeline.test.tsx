import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { RequestTimeline } from './RequestTimeline'

describe('RequestTimeline', () => {
  it('retains portal communication, system events, and WhatsApp delivery', () => {
    render(<RequestTimeline history={[]} messages={[{ id: 'manager', requestId: 'request', author: { id: 'manager', fullName: 'Ana', isManager: true }, content: 'Visita agendada', channel: 'Portal', createdAt: '2026-08-10T17:30:00Z', whatsAppDelivery: { status: 'Delivered' } }, { id: 'resident', requestId: 'request', author: { id: 'resident', fullName: 'Maria' }, content: 'Estarei em casa', channel: 'Portal', createdAt: '2026-08-10T17:31:00Z' }, { id: 'system', requestId: 'request', author: { id: 'manager', fullName: 'Ana', isManager: true }, content: 'Evento técnico', channel: 'System', createdAt: '2026-08-10T17:32:00Z' }]} />)
    expect(screen.getByText('Visita agendada')).toBeVisible(); expect(screen.getByText('Estarei em casa')).toBeVisible(); expect(screen.getByText('Evento técnico')).toBeVisible(); expect(screen.getByRole('img', { name: 'Entregue pelo WhatsApp' })).toBeVisible()
  })

  it('shows spontaneous resident updates', () => {
    render(<RequestTimeline history={[]} messages={[{ id: 'message', requestId: 'request', author: { id: 'resident', fullName: 'Maria', isManager: false }, content: 'resident update', channel: 'WhatsAppResidentUpdate', createdAt: '2026-08-10T17:30:00Z' }]} />)
    expect(screen.getByText('Atualização do morador')).toBeVisible(); expect(screen.getByText('resident update')).toBeVisible()
  })

  it('correlates resident replies with status events', () => {
    render(<RequestTimeline history={[{ id: 'history', previousStatus: 'WaitingForResident', newStatus: 'InProgress', changedByUserId: 'resident', changedByFullName: 'Maria', reason: 'reply received', createdAt: '2026-08-10T17:30:00Z', answerMessageId: 'answer' }]} messages={[{ id: 'answer', requestId: 'request', author: { id: 'resident', fullName: 'Maria', isManager: false }, content: 'gate fixed', channel: 'WhatsAppResidentUpdate', createdAt: '2026-08-10T17:30:00Z', isResidentReply: true }]} />)
    expect(screen.getByText(/Status alterado/)).toBeVisible(); expect(screen.getByText(/gate fixed/)).toBeVisible()
  })

  it('distinguishes closure events', () => {
    render(<RequestTimeline history={[{ id: 'proposal', previousStatus: 'InProgress', newStatus: 'WaitingForResidentClosure', changedByUserId: 'manager', changedByFullName: 'Ana', reason: 'delivered', createdAt: '2026-08-17T11:35:00Z' }, { id: 'automatic', previousStatus: 'WaitingForResidentClosure', newStatus: 'Resolved', changedByUserId: 'manager', changedByFullName: 'Ana', reason: 'prazo encerrado', createdAt: '2026-08-17T12:35:00Z' }]} />)
    expect(screen.getByText(/aguardando confirmação/)).toBeVisible(); expect(screen.getByText(/automaticamente/)).toBeVisible()
  })

  it('keeps provider history after removal', () => {
    render(<RequestTimeline history={[]} serviceProviderHistory={[{ id: 'linked', eventType: 'Linked', providerName: 'Cesar', providerSpecialty: 'Plumbing', changedByFullName: 'Ana', createdAt: '2026-08-17T11:35:00Z' }, { id: 'removed', eventType: 'Removed', previousName: 'Cesar', previousSpecialty: 'Plumbing', changedByFullName: 'Ana', createdAt: '2026-08-17T12:35:00Z' }]} />)
    expect(screen.getByText('Prestador vinculado')).toBeVisible(); expect(screen.getAllByText('Cesar — Plumbing')).toHaveLength(2); expect(screen.getByText('Prestador removido')).toBeVisible()
  })

  it('renders private internal notes in the same timeline', () => {
    render(<RequestTimeline history={[]} internalNotes={[{ id: 'note', content: 'Conferir câmera antes de encerrar.', author: { id: 'manager', fullName: 'Ana', isManager: true }, createdAt: '2026-08-17T12:40:00Z', updatedAt: '2026-08-17T12:45:00Z' }]} />)
    expect(screen.getByText('🔒 Nota interna')).toBeVisible(); expect(screen.getByText('Conferir câmera antes de encerrar.')).toBeVisible(); expect(screen.getByText(/Editada/)).toBeVisible()
  })
})
