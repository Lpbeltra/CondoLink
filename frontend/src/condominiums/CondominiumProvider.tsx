import { useCallback, useEffect, useMemo, useRef, useState, type PropsWithChildren } from 'react'
import { useAuth } from '../auth/AuthContext'
import { api, getErrorMessage } from '../services/api'
import { clearStoredCondominiumId, getStoredCondominiumId, resolveCurrentCondominium, storeCondominiumId } from './condominiumStorage'
import type { CondominiumContext } from './types'
import { CondominiumReactContext } from './CondominiumContext'

export function CondominiumProvider({ children }: PropsWithChildren) {
  const { user } = useAuth()
  const [condominiums, setCondominiums] = useState<CondominiumContext[]>([])
  const [currentCondominium, setCurrentCondominium] = useState<CondominiumContext | null>(null)
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const requestVersion = useRef(0)

  const clearContext = useCallback(() => {
    requestVersion.current += 1
    setCondominiums([])
    setCurrentCondominium(null)
    setError(null)
    setIsLoading(false)
    clearStoredCondominiumId()
  }, [])

  const refreshCondominiums = useCallback(async () => {
    if (!user) return

    const version = ++requestVersion.current

    setIsLoading(true)
    setError(null)
    setCondominiums([])
    setCurrentCondominium(null)

    try {
      const { data } = await api.get<CondominiumContext[]>('/users/me/condominiums')
      if (version !== requestVersion.current) return
      const selected = resolveCurrentCondominium(data, getStoredCondominiumId())

      setCondominiums(data)
      setCurrentCondominium(selected)

      if (selected) storeCondominiumId(selected.condominium.id)
      else clearStoredCondominiumId()
    } catch (requestError) {
      if (version !== requestVersion.current) return
      setError(getErrorMessage(requestError))
    } finally {
      if (version === requestVersion.current) setIsLoading(false)
    }
  }, [user])

  useEffect(() => {
    if (!user) {
      clearContext()
      return
    }
    void refreshCondominiums()
  }, [clearContext, refreshCondominiums, user])

  const selectCondominium = useCallback((id: string) => {
    const selected = condominiums.find((item) => item.condominium.id === id)
    if (!selected) return
    setCurrentCondominium(selected)
    storeCondominiumId(selected.condominium.id)
  }, [condominiums])

  // These flags only adapt the interface. The API remains responsible for authorization.
  const isManager = currentCondominium?.roles.includes('Manager') ?? false
  const isResident = currentCondominium?.roles.includes('Resident') ?? false

  const value = useMemo(() => ({
    condominiums,
    currentCondominium,
    isLoading,
    error,
    selectCondominium,
    refreshCondominiums,
    isManager,
    isResident,
  }), [condominiums, currentCondominium, error, isLoading, isManager, isResident, refreshCondominiums, selectCondominium])

  return <CondominiumReactContext.Provider value={value}>{children}</CondominiumReactContext.Provider>
}
