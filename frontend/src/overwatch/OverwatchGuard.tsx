import { useMediaQuery, useTheme } from '@mui/material'
import { Navigate, Outlet } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { LoadingScreen } from '../components/LoadingScreen'
import { getOverwatchRouteAccess } from '../auth/routeAccess'
import { useManagementContext } from '../management/ManagementContext'
import { useCondominium } from '../condominiums/CondominiumContext'
import type { CondominiumRole } from '../condominiums/types'
import { getManagementEntryDestination } from '../layout/navigation'
import { OverwatchDesktopOnlyNotice } from './OverwatchDesktopOnlyNotice'

export function OverwatchGuard() {
  const { user, isInitializing } = useAuth()
  const mobile = useMediaQuery(useTheme().breakpoints.down('md'))
  const management = useManagementContext()
  const { currentCondominium } = useCondominium()
  const roles = management.managementRoles?.length
    ? management.managementRoles
    : currentCondominium?.roles ?? []
  const managementEntry = management.condominiumCount > 0
    ? getManagementEntryDestination(
      true,
      roles as CondominiumRole[],
      user?.roles,
      management.subManagerPermissions,
    )
    : null
  const access = getOverwatchRouteAccess(isInitializing, user, {
    mobile,
    managementLoading: management.isLoading || management.isSwitching,
    managementEntry,
  })

  if (access === 'loading') return <LoadingScreen />
  if (access === 'home') return <Navigate to="/" replace />
  if (access === 'management') return <Navigate to={managementEntry!} replace />
  if (access === 'desktop-only') return <OverwatchDesktopOnlyNotice />
  return <Outlet />
}
