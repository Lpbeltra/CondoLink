import { describe, expect, it } from 'vitest'
import type { CondominiumMember } from './types'
import { getPersonBadges } from './peoplePresentation'

function person(
  overrides: Partial<CondominiumMember> = {},
): CondominiumMember {
  return {
    membershipId: 'membership',
    userId: 'user',
    fullName: 'Pessoa',
    email: 'pessoa@example.com',
    phoneNumber: null,
    userActive: true,
    mustChangePassword: false,
    emailDeliveryEnabled: true,
    firstAccessStatus: 'Completed',
    lastLoginAt: '2026-07-28T10:00:00Z',
    membershipActive: true,
    joinedAt: '2026-07-28T09:00:00Z',
    endedAt: null,
    roles: ['Resident'],
    unitLinks: [],
    ...overrides,
  }
}

describe('People access badges', () => {
  it('shows active for a regular active account', () => {
    expect(getPersonBadges(person()).map(item => item.label))
      .toEqual(['Ativo'])
  })

  it('shows never logged in and temporary password when applicable', () => {
    expect(getPersonBadges(person({
      lastLoginAt: null,
      mustChangePassword: true,
    })).map(item => item.label)).toEqual([
      'Ativo',
      'Nunca acessou',
      'Senha temporária',
    ])
  })

  it('shows inactive and ended membership independently', () => {
    expect(getPersonBadges(person({
      userActive: false,
      membershipActive: false,
    })).map(item => item.label)).toEqual([
      'Inativo',
      'Vínculo encerrado',
    ])
  })
})
