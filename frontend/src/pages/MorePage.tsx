import { Card, List, ListItemButton, ListItemIcon, ListItemText, Typography } from '@mui/material'
import { useNavigate } from 'react-router-dom'
import { EmptyState } from '../components/EmptyState'
import { PageContainer } from '../components/PageContainer'
import { useManagementContext } from '../management/ManagementContext'
import { useCondominium } from '../condominiums/CondominiumContext'
import { getMoreNavigationItems } from '../layout/navigation'
import { useAuth } from '../auth/AuthContext'

export function MorePage() {
  const navigate = useNavigate()
  const { managementRoles, subManagerPermissions } = useManagementContext()
  const { currentCondominium } = useCondominium()
  const { user } = useAuth()
  const roles = (managementRoles?.length ? managementRoles : currentCondominium?.roles ?? []) as never
  const links = getMoreNavigationItems(roles, user?.roles ?? [], subManagerPermissions)

  return <PageContainer>
    <Typography variant="h1">Mais</Typography>
    <Typography color="text.secondary" mt={0.5}>Outros módulos disponíveis para seu perfil.</Typography>
    {links.length > 0
      ? <Card elevation={0} sx={{ mt: 2 }}>
          <List disablePadding aria-label="Outros módulos">
            {links.map(({ label, path, icon: Icon }) =>
              <ListItemButton key={path} onClick={() => navigate(path)} sx={{ minHeight: 52 }}>
                <ListItemIcon sx={{ minWidth: 42 }}><Icon color="primary" /></ListItemIcon>
                <ListItemText primary={label} primaryTypographyProps={{ fontWeight: 700 }} />
              </ListItemButton>)}
          </List>
        </Card>
      : <EmptyState title="Nada por aqui" description="Esta área reúne atalhos de gestão do condomínio." />}
  </PageContainer>
}
