import AddRoundedIcon from '@mui/icons-material/AddRounded'
import ForumRoundedIcon from '@mui/icons-material/ForumRounded'
import SupportAgentRoundedIcon from '@mui/icons-material/SupportAgentRounded'
import { Box, Button, Chip, Paper, Skeleton, Stack, Typography } from '@mui/material'
import WavingHandRoundedIcon from '@mui/icons-material/WavingHandRounded'
import { useAuth } from '../auth/AuthContext'
import { PageContainer } from '../components/PageContainer'
import { useCondominium } from '../condominiums/CondominiumContext'
import { getAccessMessage } from '../condominiums/presentation'
import { useNavigate } from 'react-router-dom'
import { useManagementContext } from '../management/ManagementContext'
import { ManagementCondominiumSwitcher } from '../management/components/ManagementCondominiumSwitcher'
import { managementHomeState } from '../management/contextState'

export function HomePage() {
  const { user } = useAuth()
  const navigate = useNavigate()
  const { currentCondominium, isResident } = useCondominium()
  const {
    activeCondominium,
    condominiumCount,
    isLoading: isManagementLoading,
  } = useManagementContext()
  const firstName = user?.fullName.trim().split(' ')[0]
  const hasManagerIdentity = user?.roles?.includes('Manager') ?? false
  const hasManagementAccess = condominiumCount > 0
  const accessMessage = getAccessMessage(false, isResident)
  const managementState = managementHomeState(
    condominiumCount,
    activeCondominium,
  )

  if (isManagementLoading) {
    return (
      <PageContainer>
        <Skeleton variant="rounded" height={280} />
      </PageContainer>
    )
  }
  return (
    <PageContainer>
      <Paper elevation={0} sx={{ p: { xs: 3, sm: 4 }, border: '1px solid', borderColor: 'divider', background: 'linear-gradient(135deg, #fff 58%, rgba(31,94,255,.055))' }}>
        <Box display="flex" alignItems="center" gap={1} color="primary.main" mb={1.5}><WavingHandRoundedIcon /><Typography fontWeight={750}>Início</Typography></Box>
        <Typography variant="h1">Olá, {firstName}</Typography>
        {managementState.kind === 'single' && (
          <>
            <Typography variant="h2" mt={2}>{managementState.condominiumName}</Typography>
            <Typography color="text.secondary" fontSize={{ xs: '1rem', sm: '1.1rem' }} mt={1}>
              Você possui acesso à gestão deste condomínio.
            </Typography>
          </>
        )}
        {managementState.kind === 'multiple' && (
          <>
            <Typography variant="h2" mt={2}>
              Você administra {managementState.condominiumCount} condomínios.
            </Typography>
            <Box mt={2} maxWidth={360}>
              <ManagementCondominiumSwitcher />
            </Box>
          </>
        )}
        {managementState.kind === 'none' && hasManagerIdentity && (
          <Typography color="text.secondary" fontSize={{ xs: '1rem', sm: '1.1rem' }} mt={2}>
            Você não possui condomínios disponíveis para gestão.
          </Typography>
        )}
        {managementState.kind === 'none' && !hasManagerIdentity && currentCondominium && (
          <>
            <Typography variant="h2" mt={2}>
              {currentCondominium.condominium.name}
            </Typography>
            <Typography color="text.secondary" fontSize={{ xs: '1rem', sm: '1.1rem' }} mt={1}>
              {accessMessage}
            </Typography>
          </>
        )}
        {(hasManagementAccess || isResident) && (
          <Stack direction="row" flexWrap="wrap" gap={1} mt={3}>
            {isResident && <Chip label="Morador" color="primary" variant="outlined" />}
            {hasManagementAccess && <Chip label="Síndico / Gestão" color="secondary" variant="outlined" />}
          </Stack>
        )}
        <Stack direction={{ xs: 'column', sm: 'row' }} gap={1.5} mt={4}>
          <Button variant="contained" startIcon={<ForumRoundedIcon />} onClick={() => navigate('/requests')}>Ver minhas solicitações</Button>
          <Button variant="outlined" startIcon={<AddRoundedIcon />} onClick={() => navigate('/requests/new')}>Abrir solicitação</Button>
          {hasManagementAccess && <Button variant="outlined" color="secondary" startIcon={<SupportAgentRoundedIcon />} onClick={() => navigate('/management/requests')}>Ir para atendimento</Button>}
        </Stack>
      </Paper>
    </PageContainer>
  )
}
