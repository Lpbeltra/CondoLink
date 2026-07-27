import { describe, expect, it } from 'vitest'
import {
  authenticatedEntryPath,
  getProtectedRouteAccess,
  getOverwatchRouteAccess,
} from './routeAccess'
import { hydrateSessionUser } from './session'
import type { User } from './types'
import { getNavigationItems } from '../layout/navigation'

const roleClaim =
  'http://schemas.microsoft.com/ws/2008/06/identity/claims/role'

function token(payload: object) {
  const encoded = btoa(JSON.stringify(payload))
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/, '')
  return `header.${encoded}.signature`
}

function apiUser(roles?: string[]): User {
  return {
    id: 'user-id',
    fullName: 'Platform User',
    email: 'platform@example.com',
    isActive: true,
    roles,
  }
}

describe('real session restoration flow', () => {
  it('keeps Overwatch pending while authentication is initializing', () => {
    expect(getOverwatchRouteAccess(true, null)).toBe('loading')
  })

  it('keeps /overwatch allowed after hydrating the real PlatformAdmin claim', () => {
    const user = hydrateSessionUser(
      apiUser(),
      token({ [roleClaim]: 'PlatformAdmin' }),
    )

    expect(user.roles).toEqual(['PlatformAdmin'])
    expect(getOverwatchRouteAccess(false, user)).toBe('allowed')
  })

  it('preserves JWT PlatformAdmin when /users/me returns empty roles', () => {
    const user = hydrateSessionUser(
      apiUser([]),
      token({ [roleClaim]: 'PlatformAdmin' }),
    )

    expect(user.roles).toContain('PlatformAdmin')
  })

  it('shows Overwatch navigation after the PlatformAdmin session is hydrated', () => {
    const user = hydrateSessionUser(
      apiUser([]),
      token({ [roleClaim]: 'PlatformAdmin' }),
    )

    expect(getNavigationItems([], user.roles).map((item) => item.path))
      .toContain('/overwatch')
  })

  it('redirects an authenticated common user only after loading finishes', () => {
    const user = apiUser(['Resident'])
    expect(getOverwatchRouteAccess(true, user)).toBe('loading')
    expect(getOverwatchRouteAccess(false, user)).toBe('home')
  })

  it('keeps the normal login flow when there is no stored session', () => {
    expect(getProtectedRouteAccess(true, null)).toBe('loading')
    expect(getProtectedRouteAccess(false, null)).toBe('login')
  })

  it('always starts a new login at the normal home, including PlatformAdmin', () => {
    expect(authenticatedEntryPath).toBe('/')
  })
})
