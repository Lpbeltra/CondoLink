import { describe, expect, it } from 'vitest'
import {
  clearManagementRequestFilters,
  parseManagementRequestFilters,
  setManagementRequestFilter,
  syncCondominiumFilter,
} from './managementRequestFilters'

describe('management request URL filters', () => {
  it('restores valid filters after navigation or refresh', () => {
    const filters = parseManagementRequestFilters(new URLSearchParams(
      'status=Open&priority=Urgent&categoryId=cat&search=vazamento'
      + '&sort=priority&direction=asc&condominiumId=condo',
    ))

    expect(filters).toEqual({
      status: 'Open',
      priority: 'Urgent',
      categoryId: 'cat',
      search: 'vazamento',
      sort: 'priority',
      direction: 'asc',
      condominiumId: 'condo',
    })
  })

  it('ignores invalid enum filters safely', () => {
    const filters = parseManagementRequestFilters(
      new URLSearchParams('status=Invalid&priority=Low&sort=title&direction=sideways'),
    )

    expect(filters.status).toBe('')
    expect(filters.priority).toBe('')
    expect(filters.sort).toBe('createdAt')
    expect(filters.direction).toBe('desc')
  })

  it('updates and clears query state while retaining the management context', () => {
    const original = new URLSearchParams('condominiumId=condo&status=Open')
    const updated = setManagementRequestFilter(original, 'search', 'portão')
    const cleared = clearManagementRequestFilters(updated)

    expect(updated.get('search')).toBe('portão')
    expect(cleared.toString()).toBe('condominiumId=condo')
  })

  it('drops a category that may be incompatible when condominium changes', () => {
    const result = syncCondominiumFilter(
      new URLSearchParams('condominiumId=old&categoryId=cat&status=Open'),
      'new',
    )

    expect(result.get('condominiumId')).toBe('new')
    expect(result.has('categoryId')).toBe(false)
    expect(result.get('status')).toBe('Open')
  })
})
