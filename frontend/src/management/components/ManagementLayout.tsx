import { Alert, Box, Skeleton, Tab, Tabs, Typography } from '@mui/material'
import { Outlet, useLocation, useNavigate } from 'react-router-dom'

import { PageContainer } from '../../components/PageContainer'
import { useManagementContext } from '../ManagementContext'
import { ManagementCondominiumSwitcher } from './ManagementCondominiumSwitcher'

export function ManagementLayout() {
  const { condominiums, isLoading } = useManagementContext()

  const navigate = useNavigate()
  const location = useLocation()
  const isRequestsPage = location.pathname.startsWith('/management/requests')

  if (isLoading) {
    return <PageContainer><Skeleton variant="rounded" height={160} /></PageContainer>
  }

  if (condominiums.length === 0) {
    return (
      <PageContainer>
        <Alert severity="warning">
          <Typography fontWeight={800}>
            Acesso não disponível
          </Typography>

          Você não possui permissão para gerenciar este condomínio.
        </Alert>
      </PageContainer>
    )
  }

  if (isRequestsPage) return <Outlet />

  const value = location.pathname.startsWith('/management/blocks')
    ? '/management/blocks'
    : location.pathname.startsWith('/management/categories')
      ? '/management/categories'
      : location.pathname.startsWith('/management/people')
        ? '/management/people'
        : '/management/units'

  return (
    <>
      <PageContainer maxWidth={1440} pb={{ xs: 0.5, md: 1 }}>
        <Typography variant="h1">
          Gestão
        </Typography>

        <Box mt={2} mb={2}>
          <ManagementCondominiumSwitcher />
        </Box>

        <Tabs
          value={value}
          onChange={(_, path) => navigate(path)}
          variant="scrollable"
        >
          <Tab
            value="/management/units"
            label="Unidades"
          />

          <Tab
            value="/management/blocks"
            label="Blocos"
          />

          <Tab
            value="/management/categories"
            label="Categorias"
          />

          <Tab
            value="/management/people"
            label="Pessoas"
          />
        </Tabs>
      </PageContainer>

      <Box mt={{ xs: -2, md: -4 }}>
        <Outlet />
      </Box>
    </>
  )
}
