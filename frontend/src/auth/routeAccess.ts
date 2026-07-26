import { hasPlatformAdminAccess } from './permissions'
import type { User } from './types'

export type ProtectedRouteAccess = 'loading' | 'authenticated' | 'login'
export type OverwatchRouteAccess = 'loading' | 'allowed' | 'home'

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
): OverwatchRouteAccess {
  if (isInitializing) return 'loading'
  return hasPlatformAdminAccess(user) ? 'allowed' : 'home'
}
