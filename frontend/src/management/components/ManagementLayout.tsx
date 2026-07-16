import { Alert, Box, Tab, Tabs, Typography } from '@mui/material'
import { Outlet, useLocation, useNavigate } from 'react-router-dom'
import { PageContainer } from '../../components/PageContainer'
import { useCondominium } from '../../condominiums/CondominiumContext'

export function ManagementLayout() {
  const { isManager } = useCondominium(); const navigate = useNavigate(); const location = useLocation()
  if (!isManager) return <PageContainer><Alert severity="warning"><Typography fontWeight={800}>Acesso não disponível</Typography>Você não possui permissão para gerenciar este condomínio.</Alert></PageContainer>
  const value = location.pathname.startsWith('/management/categories') ? '/management/categories' : '/management/units'
  return <><PageContainer><Typography variant="h1">Gestão</Typography><Tabs value={value} onChange={(_, path) => navigate(path)} sx={{ mt: 2 }}><Tab value="/management/units" label="Unidades" /><Tab value="/management/categories" label="Categorias" /></Tabs></PageContainer><Box><Outlet /></Box></>
}
