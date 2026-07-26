import type { User } from './types'

const roleClaim =
  'http://schemas.microsoft.com/ws/2008/06/identity/claims/role'

function normalizeRoleValues(value: unknown): string[] {
  if (typeof value === 'string') {
    const role = value.trim()
    return role ? [role] : []
  }
  if (Array.isArray(value))
    return value.flatMap((item) => normalizeRoleValues(item))
  return []
}

function uniqueRoles(roles: string[]) {
  return [...new Set(roles)]
}

export function getTokenRoles(token: string): string[] {
  try {
    const payloadPart = token.split('.')[1]
    if (!payloadPart) return []
    const normalized = payloadPart
      .replace(/-/g, '+')
      .replace(/_/g, '/')
      .padEnd(Math.ceil(payloadPart.length / 4) * 4, '=')
    const bytes = Uint8Array.from(atob(normalized), (character) =>
      character.charCodeAt(0))
    const payload = JSON.parse(
      new TextDecoder().decode(bytes),
    ) as Record<string, unknown>
    return uniqueRoles([
      ...normalizeRoleValues(payload.role),
      ...normalizeRoleValues(payload.roles),
      ...normalizeRoleValues(payload[roleClaim]),
    ])
  } catch {
    return []
  }
}

export function getSessionRoles(token: string, responseRoles?: unknown) {
  return uniqueRoles([
    ...normalizeRoleValues(responseRoles),
    ...getTokenRoles(token),
  ])
}

export function hasPlatformAdminAccess(user: User | null) {
  return user?.roles?.includes('PlatformAdmin') ?? false
}
