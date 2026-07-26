import { getSessionRoles } from './permissions'
import type { User } from './types'

export function hydrateSessionUser(
  apiUser: User,
  token: string,
  additionalRoles?: unknown,
): User {
  return {
    ...apiUser,
    roles: getSessionRoles(token, [apiUser.roles, additionalRoles]),
  }
}
