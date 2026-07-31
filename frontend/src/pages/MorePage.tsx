import { Button, Card, CardContent, Stack, Typography } from '@mui/material'
import { useNavigate } from 'react-router-dom'
import { EmptyState } from '../components/EmptyState'
import { PageContainer } from '../components/PageContainer'
import { useManagementContext } from '../management/ManagementContext'

const managementLinks = [
  { label: 'Atendimento', path: '/management/requests' },
  { label: 'Gestão', path: '/management/units' },
  { label: 'Dashboard', path: '/management/dashboard' },
]

export function MorePage() {
  const navigate = useNavigate()
  const { condominiumCount } = useManagementContext()
  const hasManagementAccess = condominiumCount > 0

  return (
    <PageContainer>
      <Typography variant="h1">Mais</Typography>
      {hasManagementAccess ? (
        <Card elevation={0} sx={{ mt: 2 }}>
          <CardContent>
            <Stack gap={1.5}>
              {managementLinks.map((link) => (
                <Button
                  key={link.path}
                  variant="outlined"
                  onClick={() => navigate(link.path)}
                >
                  {link.label}
                </Button>
              ))}
            </Stack>
          </CardContent>
        </Card>
      ) : (
        // Previously rendered an empty bordered card for residents.
        <EmptyState
          title="Nada por aqui"
          description="Esta área reúne atalhos de gestão do condomínio."
        />
      )}
    </PageContainer>
  )
}
