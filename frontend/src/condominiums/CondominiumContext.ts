import { createContext, useContext } from 'react'
import type { CondominiumContext } from './types'

export interface CondominiumContextValue {
  condominiums: CondominiumContext[]
  currentCondominium: CondominiumContext | null
  isLoading: boolean
  error: string | null
  selectCondominium: (id: string) => void
  refreshCondominiums: () => Promise<void>
  isManager: boolean
  isResident: boolean
}

export const CondominiumReactContext = createContext<CondominiumContextValue | null>(null)

export function useCondominium() {
  const context = useContext(CondominiumReactContext)
  if (!context) throw new Error('useCondominium must be used inside CondominiumProvider.')
  return context
}
