import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  type PropsWithChildren,
} from 'react'
import { useAuth } from '../auth/AuthContext'
import { getErrorMessage } from '../services/api'
import {
  getManagementContext,
  setManagementContext,
} from './api'
import {
  ManagementReactContext,
  type ManagementContextValue,
} from './ManagementContext'
import type { ManagementCondominium } from './types'

export function ManagementContextProvider({
  children,
}: PropsWithChildren) {
  const { user } = useAuth()

  const [condominiums, setCondominiums] = useState<
    ManagementCondominium[]
  >([])
  const [activeCondominiumId, setActiveCondominiumId] = useState<
    string | null
  >(null)

  // Apenas para o carregamento inicial do contexto
  const [isLoading, setIsLoading] = useState(false)

  // Apenas para a troca de condomínio
  const [isSwitching, setIsSwitching] = useState(false)

  const [error, setError] = useState<string | null>(null)

  const requestVersion = useRef(0)

  const clearContext = useCallback(() => {
    requestVersion.current += 1

    setCondominiums([])
    setActiveCondominiumId(null)

    setIsLoading(false)
    setIsSwitching(false)

    setError(null)
  }, [])

  const refresh = useCallback(async () => {
    if (!user) {
      clearContext()
      return
    }

    const version = ++requestVersion.current

    setIsLoading(true)
    setError(null)

    try {
      const context = await getManagementContext()

      if (version !== requestVersion.current) return

      setCondominiums(context.availableCondominiums)
      setActiveCondominiumId(context.activeCondominiumId)
    } catch (requestError) {
      if (version !== requestVersion.current) return

      setError(getErrorMessage(requestError))
      setCondominiums([])
      setActiveCondominiumId(null)
    } finally {
      if (version === requestVersion.current) {
        setIsLoading(false)
      }
    }
  }, [clearContext, user])

  useEffect(() => {
    if (!user) {
      clearContext()
      return
    }

    void refresh()
  }, [clearContext, refresh, user])

  const selectCondominium = useCallback(
    async (condominiumId: string | null) => {
      const version = ++requestVersion.current
      const previousCondominiumId = activeCondominiumId
      setIsSwitching(true)
      setError(null)
      setActiveCondominiumId(null)

      try {
        const context = await setManagementContext(condominiumId)

        if (version !== requestVersion.current) return

        setCondominiums(context.availableCondominiums)
        setActiveCondominiumId(context.activeCondominiumId)
      } catch (requestError) {
        if (version !== requestVersion.current) return
        setActiveCondominiumId(previousCondominiumId)
        setError(getErrorMessage(requestError))
        throw requestError
      } finally {
        if (version === requestVersion.current) setIsSwitching(false)
      }
    },
    [activeCondominiumId]
  )

  const value = useMemo<ManagementContextValue>(
    () => ({
      condominiums,
      activeCondominiumId,
      isLoading,
      isSwitching,
      error,
      refresh,
      selectCondominium,
    }),
    [
      activeCondominiumId,
      condominiums,
      isLoading,
      isSwitching,
      error,
      refresh,
      selectCondominium,
    ]
  )

  return (
    <ManagementReactContext.Provider value={value}>
      {children}
    </ManagementReactContext.Provider>
  )
}
