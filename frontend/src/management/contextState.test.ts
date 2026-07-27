import { describe, expect, it } from 'vitest'
import { isCurrentManagementRequest, managementHomeState } from './contextState'

describe('management context presentation', () => {
  it('represents zero, one and multiple condominiums without selecting the first', () => {
    expect(managementHomeState(0, null)).toEqual({ kind: 'none' })
    expect(managementHomeState(1, {
      id: 'only',
      name: 'Único',
      isActive: true,
    })).toEqual({ kind: 'single', condominiumName: 'Único' })
    expect(managementHomeState(3, null)).toEqual({
      kind: 'multiple',
      condominiumCount: 3,
    })
  })

  it('discards a response from an older context request', () => {
    expect(isCurrentManagementRequest(1, 2)).toBe(false)
    expect(isCurrentManagementRequest(2, 2)).toBe(true)
  })
})
