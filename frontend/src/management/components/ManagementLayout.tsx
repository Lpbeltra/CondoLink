import { Alert, Box, Tab, Tabs, Typography } from '@mui/material'
import { Outlet, useLocation, useNavigate } from 'react-router-dom'

import { PageContainer } from '../../components/PageContainer'
import { useCondominium } from '../../condominiums/CondominiumContext'
import { ManagementCondominiumSwitcher } from './ManagementCondominiumSwitcher'

export function ManagementLayout() {
  const { isManager } = useCondominium()

  const navigate = useNavigate()
  const location = useLocation()

  if (!isManager) {
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

  const value = location.pathname.startsWith('/management/blocks')
    ? '/management/blocks'
    : location.pathname.startsWith('/management/categories')
      ? '/management/categories'
      : location.pathname.startsWith('/management/people')
        ? '/management/people'
        : '/management/units'

  return (
    <>
      <PageContainer pb={{ xs: 0.5, md: 1 }}>
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