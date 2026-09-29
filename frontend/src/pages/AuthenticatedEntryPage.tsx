import { Alert, useMediaQuery, useTheme } from '@mui/material'
import { Navigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { hasPlatformAdminAccess } from '../auth/permissions'
import { useCondominium } from '../condominiums/CondominiumContext'
import type { CondominiumRole } from '../condominiums/types'
import { useManagementContext } from '../management/ManagementContext'
import { useAdministrator } from '../administrator/AdministratorContext'
import { getManagementEntryDestination } from '../layout/navigation'
import { OverwatchDesktopOnlyNotice } from '../overwatch/OverwatchDesktopOnlyNotice'
import { LoadingScreen } from '../components/LoadingScreen'

/** Neutral entry only: explicit module URLs never pass through this route. */
export function AuthenticatedEntryPage() {
  const { user, isInitializing } = useAuth()
  const mobile = useMediaQuery(useTheme().breakpoints.down('md'))
  const { currentCondominium, isResident, isLoading: condominiumLoading } = useCondominium()
  const management = useManagementContext()
  const administrator = useAdministrator()

  if (isInitializing) return <LoadingScreen />
  if (hasPlatformAdminAccess(user) && !mobile) return <Navigate to="/overwatch" replace />
  if (condominiumLoading || management.isLoading || management.isSwitching) return <LoadingScreen />
  if (management.condominiumCount > 0) {
    const roles = management.managementRoles?.length ? management.managementRoles : currentCondominium?.roles ?? []
    const destination = getManagementEntryDestination(mobile, roles as CondominiumRole[], user?.roles, management.subManagerPermissions)
    if (destination) return <Navigate to={destination} replace />
    if (!hasPlatformAdminAccess(user) || !mobile) return <Alert severity="info">Nenhum módulo de gestão disponível para este perfil.</Alert>
  }
  if (hasPlatformAdminAccess(user) && mobile) return <OverwatchDesktopOnlyNotice />
  if (isResident) return <Navigate to="/requests" replace />
  if (administrator.loading) return <LoadingScreen />
  if (administrator.value) return <Navigate to="/administrator/requests" replace />
  return <Alert severity="info">Nenhum condomínio disponível para este perfil.</Alert>
}
