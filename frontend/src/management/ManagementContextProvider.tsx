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
import { isCurrentManagementRequest } from './contextState'

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
  const [usesConsolidatedManagementScope, setUsesConsolidatedManagementScope] =
    useState(false)
  const [hasEligibleManagementCompany,setHasEligibleManagementCompany]=useState(false)

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
    setUsesConsolidatedManagementScope(false)
    setHasEligibleManagementCompany(false)

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
    setCondominiums([])
    setActiveCondominiumId(null)
    setUsesConsolidatedManagementScope(false)

    try {
      const context = await getManagementContext()

      if (!isCurrentManagementRequest(version, requestVersion.current)) return

      setCondominiums(context.availableCondominiums)
      setActiveCondominiumId(context.activeManagementCondominiumId)
      setUsesConsolidatedManagementScope(
        context.usesConsolidatedManagementScope,
      )
      setHasEligibleManagementCompany(Boolean(context.hasEligibleManagementCompany))
    } catch (requestError) {
      if (!isCurrentManagementRequest(version, requestVersion.current)) return

      setError(getErrorMessage(requestError))
      setCondominiums([])
      setActiveCondominiumId(null)
    } finally {
      if (isCurrentManagementRequest(version, requestVersion.current)) {
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
      const previousConsolidatedScope = usesConsolidatedManagementScope
      setIsSwitching(true)
      setError(null)
      setActiveCondominiumId(null)
      setUsesConsolidatedManagementScope(false)

      try {
        const context = await setManagementContext(condominiumId)

        if (!isCurrentManagementRequest(version, requestVersion.current)) return

        setCondominiums(context.availableCondominiums)
        setActiveCondominiumId(context.activeManagementCondominiumId)
        setUsesConsolidatedManagementScope(
          context.usesConsolidatedManagementScope,
        )
        setHasEligibleManagementCompany(Boolean(context.hasEligibleManagementCompany))
      } catch (requestError) {
        if (!isCurrentManagementRequest(version, requestVersion.current)) return
        setActiveCondominiumId(previousCondominiumId)
        setUsesConsolidatedManagementScope(previousConsolidatedScope)
        setError(getErrorMessage(requestError))
      } finally {
        if (isCurrentManagementRequest(version, requestVersion.current)) {
          setIsSwitching(false)
        }
      }
    },
    [activeCondominiumId, usesConsolidatedManagementScope]
  )

  const value = useMemo<ManagementContextValue>(
    () => {
      const activeCondominium = condominiums.find(
        item => item.id === activeCondominiumId,
      ) ?? null
      return {
        condominiums,
        activeCondominiumId,
        activeCondominium,
        condominiumCount: condominiums.length,
        usesConsolidatedManagementScope,
        hasEligibleManagementCompany,
        isLoading,
        isSwitching,
        error,
        refresh,
        selectCondominium,
      }
    },
    [
      activeCondominiumId,
      condominiums,
      isLoading,
      isSwitching,
      error,
      refresh,
      selectCondominium,
      usesConsolidatedManagementScope,
      hasEligibleManagementCompany,
    ]
  )

  return (
    <ManagementReactContext.Provider value={value}>
      {children}
    </ManagementReactContext.Provider>
  )
}
