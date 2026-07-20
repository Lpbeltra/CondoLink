import { describe, expect, it } from 'vitest'
import { hasInitialCredentials } from './onboarding'
import type { OnboardResult } from './types'

const result = (isNewUser: boolean, initialPassword: string | null): OnboardResult => ({
  user: { id: 'u', fullName: 'Pessoa', email: 'pessoa@example.com', phoneNumber: null, isActive: true },
  membership: { id: 'm', condominiumId: 'c', isActive: true, joinedAt: '' },
  roles: ['Resident'], unitMembership: null, isNewUser, initialPassword,
})

describe('temporary onboarding credentials', () => {
  it('shows credentials only for a newly created user', () => {
    expect(hasInitialCredentials(result(true, 'Temporary123'))).toBe(true)
    expect(hasInitialCredentials(result(false, null))).toBe(false)
  })
})
