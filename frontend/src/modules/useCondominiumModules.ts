import { useCallback, useEffect, useMemo, useState } from 'react'
import { useManagementContext } from '../management/ManagementContext'
import { api } from '../services/api'

export type CondominiumModule = 'Assistant' | 'Documents' | 'Providers' | 'ManagementCompanyRequests' | 'EmployeeManagement'
export interface CondominiumModuleState { module: CondominiumModule; enabled: boolean }

export function useCondominiumModules() {
  const { activeCondominiumId } = useManagementContext()
  const [modules, setModules] = useState<CondominiumModuleState[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const refresh = useCallback(async () => {
    if (!activeCondominiumId) { setModules([]); setError(''); return }
    setLoading(true); setError('')
    try { setModules((await api.get<{ modules: CondominiumModuleState[] }>('/management/modules')).data.modules) }
    catch { setModules([]); setError('Não foi possível carregar os módulos deste condomínio.') }
    finally { setLoading(false) }
  }, [activeCondominiumId])
  useEffect(() => { void refresh() }, [refresh])
  const enabled = useMemo(() => new Set(modules.filter(x => x.enabled).map(x => x.module)), [modules])
  return { modules, loading, error, refresh, isModuleEnabled: (module: CondominiumModule) => !!activeCondominiumId && enabled.has(module) }
}
