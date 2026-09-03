import { Button, Card, CardContent, Stack, Typography } from '@mui/material'
import { useNavigate } from 'react-router-dom'
import { EmptyState } from '../components/EmptyState'
import { PageContainer } from '../components/PageContainer'
import { useManagementContext } from '../management/ManagementContext'
import { useCondominium } from '../condominiums/CondominiumContext'
import { getMoreNavigationItems } from '../layout/navigation'
import { useAuth } from '../auth/AuthContext'

export function MorePage() {
  const navigate = useNavigate()
  const { condominiumCount, subManagerPermissions } = useManagementContext()
  const { currentCondominium } = useCondominium()
  const { user } = useAuth()
  const links = currentCondominium
    ? getMoreNavigationItems(currentCondominium.roles, user?.roles ?? [], subManagerPermissions)
    : []

  return <PageContainer><Typography variant="h1">Mais</Typography>{condominiumCount > 0 ? <Card elevation={0} sx={{ mt: 2 }}><CardContent><Stack gap={1.5}>{links.map(({ label, path, icon: Icon }) => <Button key={path} variant="outlined" startIcon={<Icon />} onClick={() => navigate(path)}>{label}</Button>)}</Stack></CardContent></Card> : <EmptyState title="Nada por aqui" description="Esta área reúne atalhos de gestão do condomínio." />}</PageContainer>
}
