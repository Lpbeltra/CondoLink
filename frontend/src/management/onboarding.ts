import type { OnboardResult } from './types'

export function hasInitialCredentials(result: OnboardResult) {
  return result.isNewUser && Boolean(result.initialPassword)
}
