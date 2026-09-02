import { Button, Card, CardContent, Stack, Typography } from '@mui/material'
import { useNavigate } from 'react-router-dom'
import { EmptyState } from '../components/EmptyState'
import { PageContainer } from '../components/PageContainer'
import { useManagementContext } from '../management/ManagementContext'
import { useCondominium } from '../condominiums/CondominiumContext'

const managementLinks = [
  { label: 'Atendimento', path: '/management/requests', module: 'Attendance' },
  { label: 'Gestão', path: '/management/units', module: 'Management' },
  { label: 'Dashboard', path: '/management/dashboard', module: null },
]

export function MorePage() {
  const navigate = useNavigate()
  const { condominiumCount, subManagerPermissions: rawSubManagerPermissions } = useManagementContext()
  const { currentCondominium } = useCondominium()
  const isManager = currentCondominium?.roles.includes('Manager') ?? false
  const subManagerPermissions = isManager ? ['Attendance', 'Management'] : rawSubManagerPermissions
  return <PageContainer><Typography variant="h1">Mais</Typography>{condominiumCount > 0 ? <Card elevation={0} sx={{ mt: 2 }}><CardContent><Stack gap={1.5}>{managementLinks.filter(link => !link.module || (subManagerPermissions ?? []).includes(link.module)).map(link => <Button key={link.path} variant="outlined" onClick={() => navigate(link.path)}>{link.label}</Button>)}</Stack></CardContent></Card> : <EmptyState title="Nada por aqui" description="Esta área reúne atalhos de gestão do condomínio." />}</PageContainer>
}
