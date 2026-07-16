import { describe, expect, it } from 'vitest'
import { AxiosError } from 'axios'
import { canSendMessage, filterRequestsByCondominium, formatRelativeDate, getRequestError, priorityPresentation, statusPresentation } from './presentation'
import type { RequestListItem } from './types'

const request = (id: string, condominiumId: string): RequestListItem => ({ id, condominiumId, category: { id: 'category', name: 'Manutenção' }, targetUnit: null, title: id, status: 'Open', priority: 'Normal', createdAt: '2026-07-16T12:00:00Z', updatedAt: '2026-07-16T12:00:00Z', resolvedAt: null })

describe('request presentation', () => {
  it('filters requests without changing backend order', () => {
    const filtered = filterRequestsByCondominium([request('first', 'a'), request('other', 'b'), request('last', 'a')], 'a')
    expect(filtered.map((item) => item.id)).toEqual(['first', 'last'])
  })

  it('translates statuses and priorities', () => {
    expect(statusPresentation.WaitingForResident.label).toBe('Aguardando você')
    expect(priorityPresentation.Urgent.label).toBe('Urgente')
  })

  it('formats recent updates', () => {
    expect(formatRelativeDate('2026-07-16T11:55:00Z', new Date('2026-07-16T12:00:00Z'))).toBe('Atualizada há 5 min')
  })

  it('blocks messages only for cancelled requests', () => {
    expect(canSendMessage('Cancelled')).toBe(false)
    expect(canSendMessage('Resolved')).toBe(true)
  })

  it('maps a network failure without exposing technical details', () => {
    expect(getRequestError(new AxiosError('network'))).toBe('Não foi possível carregar as informações.')
  })
})
