import { describe, expect, it } from 'vitest'
import { applySummaryFilter, selectManagementRequests, sortManagementRequests } from './managementRequests'
import type { ManagementRequestItem } from './types'

const item = (status: ManagementRequestItem['status'], title: string = status): ManagementRequestItem => ({
  id: status, condominiumId: 'condominium', condominiumName: 'Condomínio', title, status, priority: 'Normal',
  author: { id: 'user', fullName: 'Marina Silva' }, category: { id: 'category', name: 'Manutenção' },
  targetUnit: null, createdAt: '', updatedAt: '', resolvedAt: null,
})

describe('management request filters', () => {
  const requests = [item('Open'), item('InProgress'), item('Resolved'), item('Cancelled')]

  it('shows only active requests by default', () => {
    expect(selectManagementRequests(requests, '', '')).toHaveLength(2)
  })

  it('allows selecting a closed status explicitly', () => {
    expect(selectManagementRequests(requests, 'Resolved', '')).toEqual([requests[2]])
  })

  it('applies a card status, clears priority and preserves search', () => {
    expect(applySummaryFilter('InProgress', 'marina')).toEqual({ status: 'InProgress', priority: '', search: 'marina' })
  })

  it('keeps text search in the active view', () => {
    expect(selectManagementRequests([item('Open', 'Vazamento')], '', 'marina')).toHaveLength(1)
  })

  it('sorts by opening date, priority and condominium in either direction', () => {
    const first = { ...item('Open', 'A'), id: '1', createdAt: '2026-01-01', priority: 'Urgent' as const, condominiumName: 'Zeta' }
    const second = { ...item('Open', 'B'), id: '2', createdAt: '2026-02-01', priority: 'Normal' as const, condominiumName: 'Alfa' }
    expect(sortManagementRequests([first, second], 'createdAt', 'asc').map(x => x.id)).toEqual(['1', '2'])
    expect(sortManagementRequests([first, second], 'priority', 'desc').map(x => x.id)).toEqual(['1', '2'])
    expect(sortManagementRequests([first, second], 'condominium', 'asc').map(x => x.id)).toEqual(['2', '1'])
  })
})
