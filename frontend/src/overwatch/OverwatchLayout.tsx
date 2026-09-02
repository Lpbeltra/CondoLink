import { Suspense } from 'react'
import { Box, Skeleton, Toolbar } from '@mui/material'
import { Outlet } from 'react-router-dom'
import { AppHeader } from '../layout/AppHeader'
import { PageContainerScope } from '../components/PageContainer'
import { OverwatchMobileNavigation } from './OverwatchMobileNavigation'
import { OverwatchSidebar } from './OverwatchSidebar'

export function OverwatchLayout() {
  return (
    <Box minHeight="100dvh" display="flex">
      <AppHeader />
      <OverwatchSidebar />
      <Box
        component="main"
        flex={1}
        minWidth={0}
        pb={{ xs: 9, md: 0 }}
        sx={{ overflowX: 'hidden' }}
      >
        <Toolbar sx={{ minHeight: { xs: '64px !important', md: '72px !important' } }} />
        <PageContainerScope fullWidth>
          <Suspense fallback={<Skeleton variant="rounded" height={240} sx={{ m: 2 }} />}>
            <Outlet />
          </Suspense>
        </PageContainerScope>
      </Box>
      <OverwatchMobileNavigation />
    </Box>
  )
}
