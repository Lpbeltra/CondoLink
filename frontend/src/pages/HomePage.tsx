import AddRoundedIcon from '@mui/icons-material/AddRounded'
import ForumRoundedIcon from '@mui/icons-material/ForumRounded'
import { Box, Button, Chip, Paper, Stack, Typography } from '@mui/material'
import WavingHandRoundedIcon from '@mui/icons-material/WavingHandRounded'
import { useAuth } from '../auth/AuthContext'
import { PageContainer } from '../components/PageContainer'
import { useCondominium } from '../condominiums/CondominiumContext'
import { getAccessMessage } from '../condominiums/presentation'
import { useNavigate } from 'react-router-dom'

export function HomePage() {
  const { user } = useAuth()
  const navigate = useNavigate()
  const { currentCondominium, isManager, isResident } = useCondominium()
  const firstName = user?.fullName.trim().split(' ')[0]
  const accessMessage = getAccessMessage(isManager, isResident)
  return (
    <PageContainer>
      <Paper elevation={0} sx={{ p: { xs: 3, sm: 4.5 }, border: '1px solid', borderColor: 'divider', background: 'linear-gradient(135deg, #fff 50%, rgba(31,94,255,.055))' }}>
        <Box display="flex" alignItems="center" gap={1} color="primary.main" mb={1.5}><WavingHandRoundedIcon /><Typography fontWeight={750}>Início</Typography></Box>
        <Typography variant="h1">Olá, {firstName}</Typography>
        <Typography variant="h2" mt={2}>{currentCondominium?.condominium.name}</Typography>
        <Typography color="text.secondary" fontSize={{ xs: '1rem', sm: '1.1rem' }} mt={1}>{accessMessage}</Typography>
        {(isManager || isResident) && (
          <Stack direction="row" flexWrap="wrap" gap={1} mt={3}>
            {isResident && <Chip label="Morador" color="primary" variant="outlined" />}
            {isManager && <Chip label="Síndico / Gestão" color="secondary" variant="outlined" />}
          </Stack>
        )}
        <Stack direction={{ xs: 'column', sm: 'row' }} gap={1.5} mt={4}>
          <Button variant="contained" startIcon={<ForumRoundedIcon />} onClick={() => navigate('/requests')}>Ver minhas solicitações</Button>
          <Button variant="outlined" startIcon={<AddRoundedIcon />} onClick={() => navigate('/requests/new')}>Abrir solicitação</Button>
        </Stack>
      </Paper>
    </PageContainer>
  )
}
