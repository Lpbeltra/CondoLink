import { useCallback, useEffect, useMemo, useState, type PropsWithChildren } from 'react'
import { api } from '../services/api'
import { AuthContext } from './AuthContext'
import type {
  LoginResponse,
  PasswordChangeRequiredResponse,
  User,
} from './types'
import { getStoredToken } from './authStorage'
import { hydrateSessionUser } from './session'
import { isTokenExpired } from './tokenExpiry'
import { refreshAccessToken, setAccessToken } from '../services/api'
import { deactivateCurrentPushSubscription } from '../pwa/webPush'

function setAuthorization(token: string | null) {
  setAccessToken(token)
}

export function AuthProvider({ children }: PropsWithChildren) {
  const [user, setUser] = useState<User | null>(null)
  const [isInitializing, setIsInitializing] = useState(true)

  const clearSession = useCallback(() => {
    setAuthorization(null)
    setUser(null)
  }, [])
  const logout = useCallback(async () => {
    try {
      await deactivateCurrentPushSubscription()
    } catch (error) {
      console.warn('[PWA] Não foi possível remover a subscription no logout.', error)
    }
    void api.post('/auth/logout', undefined, { _refreshRetried: true } as never).catch(() => undefined)
    clearSession()
  }, [clearSession])

  useEffect(() => {
    const handleUnauthorized = () => { void logout() }
    window.addEventListener('condolink:unauthorized', handleUnauthorized)
    return () => window.removeEventListener('condolink:unauthorized', handleUnauthorized)
  }, [logout])

  useEffect(() => {
    const restoreSession = async () => {
      const token = getStoredToken()
      try {
        const activeToken = !token || isTokenExpired(token) ? await refreshAccessToken() : token
        setAuthorization(activeToken)
        const { data } = await api.get<User>('/users/me')
        setUser(hydrateSessionUser(data, activeToken))
      } catch {
        setAuthorization(null); setUser(null)
      } finally {
        setIsInitializing(false)
      }
    }
    void restoreSession()
  }, [logout])

  const completeLogin = useCallback(async (data: LoginResponse) => {
      setAuthorization(data.accessToken)
      const currentUser = await api.get<User>('/users/me')
      setUser(hydrateSessionUser(
        currentUser.data,
        data.accessToken,
        data.user.roles,
      ))
  }, [])

  const login = useCallback(async (email: string, password: string) => {
    // If a different account is entered in the same browser, remove any
    // subscription still associated with the previous authenticated session.
    await deactivateCurrentPushSubscription().catch(error =>
      console.warn('[PWA] Não foi possível limpar a subscription anterior.', error))
    clearSession()
    try {
      const { data } = await api.post<
        LoginResponse | PasswordChangeRequiredResponse
      >('/auth/login', { email, password })
      if (data.requiresPasswordChange) {
        return {
          requiresPasswordChange: true as const,
          email: data.email,
          temporaryPassword: password,
        }
      }
      await completeLogin(data)
      return { requiresPasswordChange: false as const }
    } catch (error) {
      clearSession()
      throw error
    }
  }, [clearSession, completeLogin])

  const value = useMemo(() => ({
    user,
    isInitializing,
    login,
    logout,
  }), [
    isInitializing,
    login,
    logout,
    user,
  ])
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
