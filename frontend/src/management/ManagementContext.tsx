import { createContext, useContext } from 'react'
import type { ManagementCondominium } from './types'

export interface ManagementContextValue {
  condominiums: ManagementCondominium[]
  activeCondominiumId: string | null
  activeCondominium: ManagementCondominium | null
  condominiumCount: number
  usesConsolidatedManagementScope: boolean
  hasEligibleManagementCompany: boolean

  isLoading: boolean
  isSwitching: boolean
  error: string | null

  refresh(): Promise<void>
  selectCondominium(condominiumId: string | null): Promise<void>
}

export const ManagementReactContext =
  createContext<ManagementContextValue | null>(null)

export function useOptionalManagementContext() {
  return useContext(ManagementReactContext)
}

export function useManagementContext() {
  const context = useOptionalManagementContext()

  if (!context) {
    throw new Error(
      'useManagementContext must be used inside a ManagementContextProvider.'
    )
  }

  return context
}
