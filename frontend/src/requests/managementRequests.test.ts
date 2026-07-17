import { describe, expect, it } from 'vitest'
import { applySummaryFilter, selectManagementRequests } from './managementRequests'
import type { ManagementRequestItem } from './types'

const item = (status: ManagementRequestItem['status'], title: string = status): ManagementRequestItem => ({
  id: status, condominiumId: 'condominium', title, status, priority: 'Normal',
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
})
