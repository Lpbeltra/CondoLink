import { Navigate, Outlet } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { LoadingScreen } from '../components/LoadingScreen'
import { getOverwatchRouteAccess } from '../auth/routeAccess'

export function OverwatchGuard() {
  const { user, isInitializing } = useAuth()
  const access = getOverwatchRouteAccess(isInitializing, user)
  if (access === 'loading') return <LoadingScreen />
  return access === 'allowed' ? <Outlet /> : <Navigate to="/" replace />
}
