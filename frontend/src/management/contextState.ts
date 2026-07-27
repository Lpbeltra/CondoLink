import type { ManagementCondominium } from './types'

export type ManagementHomeState =
  | { kind: 'none' }
  | { kind: 'single'; condominiumName: string }
  | { kind: 'multiple'; condominiumCount: number }

export function managementHomeState(
  condominiumCount: number,
  activeCondominium: ManagementCondominium | null,
): ManagementHomeState {
  if (condominiumCount === 0) return { kind: 'none' }
  if (condominiumCount === 1 && activeCondominium) {
    return {
      kind: 'single',
      condominiumName: activeCondominium.name,
    }
  }
  return { kind: 'multiple', condominiumCount }
}

export function isCurrentManagementRequest(
  requestVersion: number,
  currentVersion: number,
) {
  return requestVersion === currentVersion
}
