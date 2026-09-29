import { hasPlatformAdminAccess } from './permissions'
import type { User } from './types'

export type ProtectedRouteAccess = 'loading' | 'authenticated' | 'login'
export type OverwatchRouteAccess = 'loading' | 'allowed' | 'home' | 'management' | 'desktop-only'
export const authenticatedEntryPath = '/'

export function authenticationReturnPath(state: unknown): string {
  const from = (state as { from?: unknown } | null)?.from
  return typeof from === 'string' && from.startsWith('/') && !from.startsWith('//')
    && !from.includes('\\') && !/^\/(login|change-password|primeiro-acesso)(?:[/?#]|$)/.test(from)
    ? from : authenticatedEntryPath
}

export function getProtectedRouteAccess(
  isInitializing: boolean,
  user: User | null,
): ProtectedRouteAccess {
  if (isInitializing) return 'loading'
  return user ? 'authenticated' : 'login'
}

export function getOverwatchRouteAccess(
  isInitializing: boolean,
  user: User | null,
  options: { mobile?: boolean; managementLoading?: boolean; managementEntry?: string | null } = {},
): OverwatchRouteAccess {
  if (isInitializing) return 'loading'
  if (!hasPlatformAdminAccess(user)) return 'home'
  if (!options.mobile) return 'allowed'
  if (options.managementLoading) return 'loading'
  return options.managementEntry ? 'management' : 'desktop-only'
}
