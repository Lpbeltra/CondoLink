import { useCallback, useState } from 'react'
import ArrowBackRoundedIcon from '@mui/icons-material/ArrowBackRounded'
import PowerSettingsNewRoundedIcon from '@mui/icons-material/PowerSettingsNewRounded'
import EditRoundedIcon from '@mui/icons-material/EditRounded'
import {
  Alert, Box, Button, Card, CardContent, Chip, CircularProgress, Dialog,
  DialogActions, DialogContent, DialogTitle, Divider, Skeleton, Stack,
  Tab, Tabs, Typography,
} from '@mui/material'
import { useNavigate, useParams } from 'react-router-dom'
import { EmptyState } from '../../components/EmptyState'
import { PageContainer } from '../../components/PageContainer'
import { TransientFeedback } from '../../components/TransientFeedback'
import { useGuardedLoad } from '../../components/useGuardedLoad'
import { formatDateTime } from '../../requests/presentation'
import { getManager, updateManager, updateManagerStatus } from '../managers/api'
import { managerError } from '../managers/errors'
import { ManagerRelationships } from '../managers/ManagerRelationships'
import { managerDetailTabs } from '../managers/presentation'
import type { ManagerInput } from '../managers/types'
import { ManagerFormDialog } from '../managers/ManagerFormDialog'
import { formatCnpj, formatCpf } from '../registration'

type DetailTab = 'overview' | 'condominiums' | 'settings'

export function OverwatchManagerDetailsPage() {
  const { managerId = '' } = useParams()
  const navigate = useNavigate()
  const [isSaving, setIsSaving] = useState(false)
  const [tab, setTab] = useState<DetailTab>('overview')
  const [statusOpen, setStatusOpen] = useState(false)
  const [editOpen, setEditOpen] = useState(false)
  const [feedback, setFeedback] = useState('')

  // Guarded so a slow response for a previous managerId cannot land in state
  // and have a later save write to the wrong record.
  const fetchManager = useCallback(() => getManager(managerId), [managerId])
  const {
    data: manager,
    isLoading,
    error,
    setData: setManager,
    setError,
  } = useGuardedLoad(fetchManager, managerError)

  const refreshDetails = useCallback(async () => {
    try {
      setManager(await getManager(managerId))
    } catch (requestError) {
      setError(managerError(requestError))
    }
  }, [managerId, setError, setManager])

  const changeStatus = async () => {
    if (!manager || isSaving) return
    setIsSaving(true)
    setError('')
    try {
      const result = await updateManagerStatus(manager.id, !manager.isActive)
      setManager({ ...manager, isActive: result.isActive, updatedAt: result.updatedAt })
      setStatusOpen(false)
      setFeedback(result.isActive ? 'Síndico ativado.' : 'Síndico inativado.')
    } catch (requestError) {
      setError(managerError(requestError))
    } finally {
      setIsSaving(false)
    }
  }

  const save = async (input: ManagerInput) => {
    if (!manager || isSaving) return
    setIsSaving(true)
    try {
      setManager(await updateManager(manager.id, input))
      setEditOpen(false)
      setFeedback('Síndico atualizado.')
    } catch (requestError) {
      setError(managerError(requestError))
    } finally {
      setIsSaving(false)
    }
  }

  if (isLoading) {
    return <PageContainer><Skeleton variant="rounded" height={280} /></PageContainer>
  }
  if (!manager) {
    return (
      <PageContainer>
        {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
        <EmptyState title="Síndico não encontrado"
          description="Verifique o endereço ou volte para a listagem."
          actionLabel="Voltar para síndicos"
          onAction={() => navigate('/overwatch/managers')} />
      </PageContainer>
    )
  }

  const overview = [
    ['Nome', manager.fullName],
    ['E-mail', manager.email],
    ['Telefone / WhatsApp', manager.phoneNumber || 'Não informado'],
    ['CPF', formatCpf(manager.cpf)],
    ['CNPJ', formatCnpj(manager.cnpj)],
    ['Endereço', manager.address || 'Não informado'],
    ['Cidade', manager.city || 'Não informada'],
    ['Estado', manager.state || 'Não informado'],
    ['Status', manager.isActive ? 'Ativo' : 'Inativo'],
    ['Condomínios', manager.condominiumCount],
    ['Criado em', formatDateTime(manager.createdAt)],
    ['Última atualização', formatDateTime(manager.updatedAt)],
  ]

  return (
    <PageContainer>
      <Button startIcon={<ArrowBackRoundedIcon />}
        onClick={() => navigate('/overwatch/managers')}>Voltar</Button>
      <Stack direction={{ xs: 'column', md: 'row' }} justifyContent="space-between"
        gap={2} mt={2}>
        <Box>
          <Stack direction="row" alignItems="center" gap={1.5} flexWrap="wrap">
            <Typography variant="h1" sx={{ overflowWrap: 'anywhere' }}>
              {manager.fullName}
            </Typography>
            <Chip color={manager.isActive ? 'success' : 'default'}
              label={manager.isActive ? 'Ativo' : 'Inativo'} />
          </Stack>
          <Typography
            color="text.secondary"
            mt={1}
            sx={{ overflowWrap: 'anywhere' }}
          >
            {manager.email}
          </Typography>
        </Box>
        <Stack direction="row" gap={1}>
          <Button variant="outlined" startIcon={<EditRoundedIcon />}
            onClick={() => setEditOpen(true)}>Editar</Button>
          <Button variant="contained" color={manager.isActive ? 'error' : 'primary'}
            startIcon={<PowerSettingsNewRoundedIcon />} onClick={() => setStatusOpen(true)}>
            {manager.isActive ? 'Inativar' : 'Ativar'}
          </Button>
        </Stack>
      </Stack>
      {error && <Alert severity="error" sx={{ mt: 2 }}>{error}</Alert>}
      <Card elevation={0} sx={{ mt: 3 }}>
        <Tabs value={tab} onChange={(_, value: DetailTab) => setTab(value)}
          variant="scrollable" scrollButtons="auto" aria-label="Seções do síndico">
          {managerDetailTabs.map((item) => (
            <Tab key={item.value} value={item.value} label={item.label} />
          ))}
        </Tabs>
        <Divider />
        <CardContent sx={{ p: { xs: 2, sm: 3 } }}>
          {tab === 'overview' && (
            <Box display="grid"
              gridTemplateColumns={{ xs: '1fr', sm: 'repeat(2, minmax(0, 1fr))' }}
              gap={3}>
              {overview.map(([label, value]) => (
                <Box key={label}>
                  <Typography color="text.secondary" fontSize=".78rem" fontWeight={700}>
                    {label}
                  </Typography>
                  <Typography mt={0.5}>{value}</Typography>
                </Box>
              ))}
            </Box>
          )}
          {tab === 'condominiums' && (
            <ManagerRelationships
              managerId={manager.id}
              onChanged={() => void refreshDetails()}
            />
          )}
          {tab === 'settings' && (
            <EmptyState title="Configurações em breve"
              description="Novas configurações do síndico serão implementadas em um próximo lote." />
          )}
        </CardContent>
      </Card>
      <Dialog open={statusOpen} onClose={() => !isSaving && setStatusOpen(false)}
        fullWidth maxWidth="xs">
        <DialogTitle>{manager.isActive ? 'Inativar síndico' : 'Ativar síndico'}</DialogTitle>
        <DialogContent>
          <Typography>
            {manager.isActive
              ? 'O usuário perderá o acesso ao sistema, mas seus vínculos com condomínios serão preservados.'
              : 'O usuário voltará a ter acesso ao sistema. Seus vínculos existentes serão mantidos.'}
          </Typography>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setStatusOpen(false)} disabled={isSaving}>Cancelar</Button>
          <Button variant="contained" color={manager.isActive ? 'error' : 'primary'}
            disabled={isSaving} onClick={() => void changeStatus()}>
            {isSaving ? <CircularProgress size={20} color="inherit" /> : 'Confirmar'}
          </Button>
        </DialogActions>
      </Dialog>
      <TransientFeedback message={feedback} severity="success"
        onClose={() => setFeedback('')} />
      <ManagerFormDialog open={editOpen} manager={manager} isSaving={isSaving}
        error={error} onClose={() => setEditOpen(false)} onSubmit={save} />
    </PageContainer>
  )
}
