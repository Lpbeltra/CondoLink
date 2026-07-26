import { useCallback, useEffect, useState } from 'react'
import ArrowBackRoundedIcon from '@mui/icons-material/ArrowBackRounded'
import EditRoundedIcon from '@mui/icons-material/EditRounded'
import PowerSettingsNewRoundedIcon from '@mui/icons-material/PowerSettingsNewRounded'
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  Skeleton,
  Stack,
  Tab,
  Tabs,
  Typography,
} from '@mui/material'
import { useNavigate, useParams } from 'react-router-dom'
import { EmptyState } from '../../components/EmptyState'
import { PageContainer } from '../../components/PageContainer'
import { TransientFeedback } from '../../components/TransientFeedback'
import { formatDateTime } from '../../requests/presentation'
import {
  getOverwatchCondominium,
  listManagementCompanyOptions,
  setCondominiumManagementCompany,
  updateOverwatchCondominium,
  updateOverwatchCondominiumStatus,
} from '../condominiums/api'
import { condominiumError } from '../condominiums/errors'
import { CondominiumFormDialog } from '../condominiums/CondominiumFormDialog'
import { CondominiumManagers } from '../condominiums/CondominiumManagers'
import { condominiumDetailTabs } from '../condominiums/presentation'
import type {
  CondominiumInput,
  ManagementCompanyOption,
  OverwatchCondominium,
} from '../condominiums/types'

type DetailTab = 'overview' | 'managers' | 'settings'

interface SaveRequest {
  input: CondominiumInput
  managementCompanyId: string | null
}

export function OverwatchCondominiumDetailsPage() {
  const { condominiumId = '' } = useParams()
  const navigate = useNavigate()
  const [condominium, setCondominium] = useState<OverwatchCondominium | null>(null)
  const [managementCompanies, setManagementCompanies] =
    useState<ManagementCompanyOption[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState('')
  const [operationError, setOperationError] = useState('')
  const [tab, setTab] = useState<DetailTab>('overview')
  const [editOpen, setEditOpen] = useState(false)
  const [statusOpen, setStatusOpen] = useState(false)
  const [isSaving, setIsSaving] = useState(false)
  const [feedback, setFeedback] = useState('')

  const load = useCallback(async () => {
    setIsLoading(true)
    setError('')
    try {
      const [condominiumData, companyData] = await Promise.all([
        getOverwatchCondominium(condominiumId),
        listManagementCompanyOptions(),
      ])
      setCondominium(condominiumData)
      setManagementCompanies(companyData)
    } catch (requestError) {
      setError(condominiumError(requestError))
    } finally {
      setIsLoading(false)
    }
  }, [condominiumId])

  useEffect(() => {
    void load()
  }, [load])

  const refreshDetails = useCallback(async () => {
    try {
      setCondominium(await getOverwatchCondominium(condominiumId))
    } catch (requestError) {
      setError(condominiumError(requestError))
    }
  }, [condominiumId])

  const save = async ({ input, managementCompanyId }: SaveRequest) => {
    if (!condominium || isSaving) return
    setIsSaving(true)
    setOperationError('')
    try {
      await updateOverwatchCondominium(condominium.id, input)
      if (managementCompanyId !== condominium.managementCompanyId) {
        await setCondominiumManagementCompany(
          condominium.id,
          managementCompanyId,
        )
      }
      const saved = await getOverwatchCondominium(condominium.id)
      setCondominium(saved)
      setEditOpen(false)
      setFeedback('Condomínio atualizado.')
    } catch (requestError) {
      setOperationError(condominiumError(requestError))
    } finally {
      setIsSaving(false)
    }
  }

  const changeStatus = async () => {
    if (!condominium || isSaving) return
    setIsSaving(true)
    setOperationError('')
    try {
      await updateOverwatchCondominiumStatus(
        condominium.id,
        !condominium.isActive,
      )
      const saved = await getOverwatchCondominium(condominium.id)
      setCondominium(saved)
      setStatusOpen(false)
      setFeedback(saved.isActive ? 'Condomínio ativado.' : 'Condomínio inativado.')
    } catch (requestError) {
      setOperationError(condominiumError(requestError))
    } finally {
      setIsSaving(false)
    }
  }

  if (isLoading) {
    return (
      <PageContainer>
        <Skeleton width={180} />
        <Skeleton variant="rounded" height={240} sx={{ mt: 2 }} />
      </PageContainer>
    )
  }

  if (!condominium) {
    return (
      <PageContainer>
        {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
        <EmptyState
          title="Condomínio não encontrado"
          description="Verifique o endereço ou volte para a listagem."
          actionLabel="Voltar para condomínios"
          onAction={() => navigate('/overwatch/condominiums')}
        />
      </PageContainer>
    )
  }

  const overview = [
    ['Nome', condominium.name],
    ['E-mail', condominium.email || 'Não informado'],
    ['Telefone', condominium.phoneNumber || 'Não informado'],
    ['Administradora', condominium.managementCompanyName || 'Sem administradora'],
    ['Síndicos ativos', condominium.managerCount],
    ['Status', condominium.isActive ? 'Ativo' : 'Inativo'],
    ['Criado em', formatDateTime(condominium.createdAt)],
    ['Última atualização', formatDateTime(condominium.updatedAt)],
  ]

  return (
    <PageContainer>
      <Button
        startIcon={<ArrowBackRoundedIcon />}
        onClick={() => navigate('/overwatch/condominiums')}
      >
        Voltar
      </Button>

      <Stack
        direction={{ xs: 'column', md: 'row' }}
        justifyContent="space-between"
        alignItems={{ md: 'flex-start' }}
        gap={2}
        mt={2}
      >
        <Box>
          <Stack direction="row" alignItems="center" gap={1.5} flexWrap="wrap">
            <Typography variant="h1" sx={{ overflowWrap: 'anywhere' }}>
              {condominium.name}
            </Typography>
            <Chip
              color={condominium.isActive ? 'success' : 'default'}
              label={condominium.isActive ? 'Ativo' : 'Inativo'}
            />
          </Stack>
          <Typography
            color="text.secondary"
            mt={1}
            sx={{ overflowWrap: 'anywhere' }}
          >
            {condominium.managementCompanyName || 'Sem administradora vinculada'}
          </Typography>
        </Box>
        <Stack direction="row" gap={1} flexWrap="wrap">
          <Button
            variant="outlined"
            startIcon={<EditRoundedIcon />}
            onClick={() => {
              setOperationError('')
              setEditOpen(true)
            }}
          >
            Editar
          </Button>
          <Button
            variant="contained"
            color={condominium.isActive ? 'error' : 'primary'}
            startIcon={<PowerSettingsNewRoundedIcon />}
            onClick={() => {
              setOperationError('')
              setStatusOpen(true)
            }}
          >
            {condominium.isActive ? 'Inativar' : 'Ativar'}
          </Button>
        </Stack>
      </Stack>

      <Card elevation={0} sx={{ mt: 3 }}>
        <Tabs
          value={tab}
          onChange={(_, value: DetailTab) => setTab(value)}
          variant="scrollable"
          scrollButtons="auto"
          aria-label="Seções do condomínio"
        >
          {condominiumDetailTabs.map((item) => (
            <Tab key={item.value} value={item.value} label={item.label} />
          ))}
        </Tabs>
        <Divider />
        <CardContent sx={{ p: { xs: 2, sm: 3 } }}>
          {tab === 'overview' && (
            <Box
              display="grid"
              gridTemplateColumns={{
                xs: '1fr',
                sm: 'repeat(2, minmax(0, 1fr))',
                lg: 'repeat(3, minmax(0, 1fr))',
              }}
              gap={3}
            >
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
          {tab === 'managers' && (
            <CondominiumManagers
              condominiumId={condominium.id}
              onChanged={() => void refreshDetails()}
            />
          )}
          {tab === 'settings' && (
            <EmptyState
              title="Configurações em breve"
              description="Novas configurações do condomínio serão implementadas em um próximo lote."
            />
          )}
        </CardContent>
      </Card>

      <CondominiumFormDialog
        open={editOpen}
        condominium={condominium}
        managementCompanies={managementCompanies}
        isSaving={isSaving}
        error={operationError}
        onClose={() => setEditOpen(false)}
        onSubmit={save}
      />

      <Dialog
        open={statusOpen}
        onClose={() => !isSaving && setStatusOpen(false)}
        fullWidth
        maxWidth="xs"
      >
        <DialogTitle>
          {condominium.isActive ? 'Inativar condomínio' : 'Ativar condomínio'}
        </DialogTitle>
        <DialogContent>
          {operationError && <Alert severity="error" sx={{ mb: 2 }}>{operationError}</Alert>}
          <Typography>
            {condominium.isActive
              ? `O condomínio ${condominium.name} ficará inativo. A administradora e os síndicos permanecerão vinculados.`
              : `O condomínio ${condominium.name} voltará a ficar ativo.`}
          </Typography>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setStatusOpen(false)} disabled={isSaving}>Cancelar</Button>
          <Button
            variant="contained"
            color={condominium.isActive ? 'error' : 'primary'}
            disabled={isSaving}
            onClick={() => void changeStatus()}
          >
            {isSaving ? <CircularProgress size={20} color="inherit" /> : 'Confirmar'}
          </Button>
        </DialogActions>
      </Dialog>

      <TransientFeedback
        message={feedback}
        severity="success"
        onClose={() => setFeedback('')}
      />
    </PageContainer>
  )
}
