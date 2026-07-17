import { useCallback, useEffect, useMemo, useState, type PropsWithChildren } from 'react'
import { api } from '../services/api'
import { AuthContext } from './AuthContext'
import type { LoginResponse, User } from './types'
import { clearStoredToken, getStoredToken, storeToken } from './authStorage'

function setAuthorization(token: string | null) {
  if (token) api.defaults.headers.common.Authorization = `Bearer ${token}`
  else delete api.defaults.headers.common.Authorization
}

export function AuthProvider({ children }: PropsWithChildren) {
  const [user, setUser] = useState<User | null>(null)
  const [isInitializing, setIsInitializing] = useState(true)

  const logout = useCallback(() => {
    clearStoredToken()
    setAuthorization(null)
    setUser(null)
  }, [])

  useEffect(() => {
    const handleUnauthorized = () => logout()
    window.addEventListener('condolink:unauthorized', handleUnauthorized)
    return () => window.removeEventListener('condolink:unauthorized', handleUnauthorized)
  }, [logout])

  useEffect(() => {
    const restoreSession = async () => {
      const token = getStoredToken()
      if (!token) {
        setIsInitializing(false)
        return
      }

      setAuthorization(token)
      try {
        const { data } = await api.get<User>('/users/me')
        setUser(data)
      } catch {
        logout()
      } finally {
        setIsInitializing(false)
      }
    }
    void restoreSession()
  }, [logout])

  const login = useCallback(async (email: string, password: string) => {
    const { data } = await api.post<LoginResponse>('/auth/login', { email, password })
    storeToken(data.accessToken)
    setAuthorization(data.accessToken)
    const currentUser = await api.get<User>('/users/me')
    setUser(currentUser.data)
  }, [])

  const value = useMemo(() => ({ user, isInitializing, login, logout }), [isInitializing, login, logout, user])
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
