import { Box, Button, Skeleton, Stack, Toolbar } from '@mui/material'
import { Outlet } from 'react-router-dom'
import { AppHeader } from './AppHeader'
import { MobileBottomNavigation } from './MobileBottomNavigation'
import { drawerWidth, Sidebar } from './Sidebar'
import { useCondominium } from '../condominiums/CondominiumContext'
import { EmptyState } from '../components/EmptyState'
import { PageContainer } from '../components/PageContainer'
import { useAuth } from '../auth/AuthContext'
import { hasPlatformAdminAccess } from '../auth/permissions'
import { useManagementContext } from '../management/ManagementContext'

export function AppShell() {
  const { currentCondominium, isLoading, error, refreshCondominiums } = useCondominium()
  const { user } = useAuth()
  const {
    condominiumCount,
    isLoading: isManagementLoading,
  } = useManagementContext()
  const hasManagementContext = condominiumCount > 0
  const hasContext = Boolean(currentCondominium) || hasManagementContext
  const showNavigation = hasContext || hasPlatformAdminAccess(user)

  const content = isLoading || isManagementLoading ? (
    <PageContainer><Stack spacing={2}><Skeleton variant="rounded" height={180} /><Skeleton width="55%" /><Skeleton width="35%" /></Stack></PageContainer>
  ) : error ? (
    <PageContainer><EmptyState title="Não foi possível carregar seus condomínios" description={error} action={<Button variant="contained" onClick={() => void refreshCondominiums()}>Tentar novamente</Button>} /></PageContainer>
  ) : !hasContext && !hasPlatformAdminAccess(user) ? (
    <PageContainer><EmptyState title="Nenhum condomínio disponível" description="Sua conta ainda não possui acesso a um condomínio. Entre em contato com o responsável pela administração." /></PageContainer>
  ) : <Outlet />

  return (
    <Box minHeight="100dvh" display="flex">
      <AppHeader />
      {showNavigation && <Sidebar />}
      <Box component="main" flex={1} minWidth={0} ml={{ md: 4 }} pb={{ xs: showNavigation ? 9 : 2, md: 0 }} sx={{ overflowX: 'hidden' }}>
        <Toolbar sx={{ minHeight: { xs: '64px !important', md: '72px !important' } }} />
        {content}
      </Box>
      {showNavigation && <MobileBottomNavigation />}
    </Box>
  )
}
