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
        sx={{
          overflowX: 'hidden',
          pb: { xs: 'calc(72px + env(safe-area-inset-bottom))', md: 0 },
        }}
      >
        <Toolbar sx={{ minHeight: {
          xs: 'calc(64px + env(safe-area-inset-top)) !important',
          md: 'calc(72px + env(safe-area-inset-top)) !important',
        } }} />
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
