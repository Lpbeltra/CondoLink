import { useCallback, useEffect, useMemo, useState, type PropsWithChildren } from 'react'
import { api } from '../services/api'
import { AuthContext } from './AuthContext'
import type { LoginResponse, User } from './types'
import { clearStoredToken, getStoredToken, storeToken } from './authStorage'
import { hydrateSessionUser } from './session'
import { getMillisecondsUntilExpiry, isTokenExpired } from './tokenExpiry'

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

      // Don't render an authenticated shell around an already-dead token.
      if (isTokenExpired(token)) {
        logout()
        setIsInitializing(false)
        return
      }

      setAuthorization(token)
      try {
        const { data } = await api.get<User>('/users/me')
        setUser(hydrateSessionUser(data, token))
      } catch {
        logout()
      } finally {
        setIsInitializing(false)
      }
    }
    void restoreSession()
  }, [logout])

  // Expire the session in place, so a tab left open doesn't keep showing a
  // logged-in UI backed by a token the API will now reject.
  useEffect(() => {
    if (!user) return
    const token = getStoredToken()
    if (!token) return
    const remaining = getMillisecondsUntilExpiry(token)
    if (remaining === null) return
    if (remaining === 0) {
      logout()
      return
    }
    const timer = window.setTimeout(logout, remaining)
    return () => window.clearTimeout(timer)
  }, [logout, user])

  const login = useCallback(async (email: string, password: string) => {
    logout()
    try {
      const { data } = await api.post<LoginResponse>('/auth/login', { email, password })
      storeToken(data.accessToken)
      setAuthorization(data.accessToken)
      const currentUser = await api.get<User>('/users/me')
      setUser(hydrateSessionUser(
        currentUser.data,
        data.accessToken,
        data.user.roles,
      ))
    } catch (error) {
      logout()
      throw error
    }
  }, [logout])

  const value = useMemo(() => ({ user, isInitializing, login, logout }), [isInitializing, login, logout, user])
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
