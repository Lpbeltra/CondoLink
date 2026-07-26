import { describe, expect, it } from 'vitest'
import { getSessionRoles, getTokenRoles, hasPlatformAdminAccess } from './permissions'

function token(payload: object) {
  const encoded = btoa(JSON.stringify(payload))
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/, '')
  return `header.${encoded}.signature`
}

describe('platform permissions', () => {
  it('reads the real .NET role claim when represented as a string', () => {
    expect(getTokenRoles(token({
      'http://schemas.microsoft.com/ws/2008/06/identity/claims/role':
        'PlatformAdmin',
    }))).toEqual(['PlatformAdmin'])
  })

  it('combines string, array and equivalent role claims without duplicates', () => {
    expect(getTokenRoles(token({
      role: 'Resident',
      roles: ['Manager', 'PlatformAdmin'],
      'http://schemas.microsoft.com/ws/2008/06/identity/claims/role':
        ['PlatformAdmin', ['Auditor']],
    }))).toEqual(['Resident', 'Manager', 'PlatformAdmin', 'Auditor'])
  })

  it('normalizes response and current-token roles into a new session', () => {
    const newToken = token({ role: 'PlatformAdmin' })
    expect(getSessionRoles(newToken, ['Manager', 'Manager']))
      .toEqual(['Manager', 'PlatformAdmin'])
  })

  it('denies Overwatch to users without PlatformAdmin', () => {
    expect(hasPlatformAdminAccess({
      id: '1',
      fullName: 'Resident',
      email: 'resident@example.com',
      isActive: true,
      roles: ['Resident'],
    })).toBe(false)
  })

  it('allows Overwatch to PlatformAdmin', () => {
    expect(hasPlatformAdminAccess({
      id: '2',
      fullName: 'Admin',
      email: 'admin@example.com',
      isActive: true,
      roles: ['PlatformAdmin'],
    })).toBe(true)
  })
})
