import { createContext, useContext } from 'react'
import type { User } from './types'

export interface AuthContextValue {
  user: User | null
  isInitializing: boolean
  login: (email: string, password: string) => Promise<void>
  logout: () => void
}

export const AuthContext = createContext<AuthContextValue | null>(null)

export function useAuth() {
  const context = useContext(AuthContext)
  if (!context) throw new Error('useAuth must be used inside AuthProvider.')
  return context
}
