import { useCallback, useState } from 'react'
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
import { useGuardedLoad } from '../../components/useGuardedLoad'
import { formatDateTime } from '../../requests/presentation'
import { formatCnpj } from '../registration'
import {
  getManagementCompany,
  updateManagementCompany,
  updateManagementCompanyStatus,
} from '../managementCompanies/api'
import { managementCompanyError } from '../managementCompanies/errors'
import { ManagementCompanyEmployees } from '../managementCompanies/ManagementCompanyEmployees'
import { ManagementCompanyFormDialog } from '../managementCompanies/ManagementCompanyFormDialog'
import { managementCompanyDetailTabs } from '../managementCompanies/presentation'
import type {
  ManagementCompany,
  ManagementCompanyInput,
} from '../managementCompanies/types'

type DetailTab = 'overview' | 'employees' | 'categories'

const overviewFields: Array<{
  label: string
  value: (company: ManagementCompany) => string | number
}> = [
  { label: 'Nome', value: (company) => company.name },
  { label: 'CNPJ', value: (company) => formatCnpj(company.cnpj) },
  { label: 'Endereço', value: (company) => company.address || 'Não informado' },
  { label: 'Cidade', value: (company) => company.city || 'Não informada' },
  { label: 'Estado', value: (company) => company.state || 'Não informado' },
  { label: 'E-mail', value: (company) => company.email || 'Não informado' },
  { label: 'Telefone', value: (company) => company.phoneNumber || 'Não informado' },
  { label: 'Status', value: (company) => company.isActive ? 'Ativa' : 'Inativa' },
  { label: 'Condomínios', value: (company) => company.condominiumCount },
  { label: 'Funcionários', value: (company) => company.employeeCount },
  { label: 'Criada em', value: (company) => formatDateTime(company.createdAt) },
  { label: 'Última atualização', value: (company) => formatDateTime(company.updatedAt) },
]

export function OverwatchManagementCompanyDetailsPage() {
  const { managementCompanyId = '' } = useParams()
  const navigate = useNavigate()
  const [tab, setTab] = useState<DetailTab>('overview')
  const [editOpen, setEditOpen] = useState(false)
  const [statusOpen, setStatusOpen] = useState(false)
  const [isSaving, setIsSaving] = useState(false)
  const [operationError, setOperationError] = useState('')
  const [feedback, setFeedback] = useState('')

  // Guarded so a slow response for a previous managementCompanyId cannot land
  // in state and have a later save write to the wrong record.
  const fetchCompany = useCallback(
    () => getManagementCompany(managementCompanyId),
    [managementCompanyId],
  )
  const {
    data: company,
    isLoading,
    error,
    setData: setCompany,
  } = useGuardedLoad(fetchCompany, managementCompanyError)

  const save = async (input: ManagementCompanyInput) => {
    if (!company || isSaving) return
    setIsSaving(true)
    setOperationError('')
    try {
      const updated = await updateManagementCompany(company.id, input)
      setCompany(updated)
      setEditOpen(false)
      setFeedback('Administradora atualizada.')
    } catch (requestError) {
      setOperationError(managementCompanyError(requestError))
    } finally {
      setIsSaving(false)
    }
  }

  const changeStatus = async () => {
    if (!company || isSaving) return
    setIsSaving(true)
    setOperationError('')
    try {
      const updated = await updateManagementCompanyStatus(
        company.id,
        !company.isActive,
      )
      setCompany({ ...company, isActive: updated.isActive })
      setStatusOpen(false)
      setFeedback(updated.isActive ? 'Administradora ativada.' : 'Administradora inativada.')
    } catch (requestError) {
      setOperationError(managementCompanyError(requestError))
    } finally {
      setIsSaving(false)
    }
  }

  if (isLoading) {
    return (
      <PageContainer>
        <Skeleton width={180} />
        <Skeleton variant="rounded" height={220} sx={{ mt: 2 }} />
      </PageContainer>
    )
  }

  if (!company) {
    return (
      <PageContainer>
        {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
        <EmptyState
          title="Administradora não encontrada"
          description="Verifique se o endereço está correto ou volte para a listagem."
          actionLabel="Voltar para administradoras"
          onAction={() => navigate('/overwatch/management-companies')}
        />
      </PageContainer>
    )
  }

  return (
    <PageContainer>
      <Button
        startIcon={<ArrowBackRoundedIcon />}
        onClick={() => navigate('/overwatch/management-companies')}
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
              {company.name}
            </Typography>
            <Chip
              color={company.isActive ? 'success' : 'default'}
              label={company.isActive ? 'Ativa' : 'Inativa'}
            />
          </Stack>
          <Typography color="text.secondary" mt={0.5}>
            {company.condominiumCount} {company.condominiumCount === 1 ? 'condomínio vinculado' : 'condomínios vinculados'}
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
            color={company.isActive ? 'error' : 'primary'}
            startIcon={<PowerSettingsNewRoundedIcon />}
            onClick={() => {
              setOperationError('')
              setStatusOpen(true)
            }}
          >
            {company.isActive ? 'Inativar' : 'Ativar'}
          </Button>
        </Stack>
      </Stack>

      <Card elevation={0} sx={{ mt: 3 }}>
        <Tabs
          value={tab}
          onChange={(_, value: DetailTab) => setTab(value)}
          variant="scrollable"
          scrollButtons="auto"
          aria-label="Seções da administradora"
        >
          {managementCompanyDetailTabs.map((item) => (
            <Tab key={item.value} value={item.value} label={item.label} />
          ))}
        </Tabs>
        <Divider />
        <CardContent sx={{ p: { xs: 2, sm: 3 } }}>
          {tab === 'overview' && (
            <Box
              display="grid"
              gridTemplateColumns={{ xs: '1fr', sm: 'repeat(2, minmax(0, 1fr))', lg: 'repeat(3, minmax(0, 1fr))' }}
              gap={3}
            >
              {overviewFields.map((field) => (
                <Box key={field.label}>
                  <Typography color="text.secondary" fontSize=".78rem" fontWeight={700}>
                    {field.label}
                  </Typography>
                  <Typography mt={0.5} sx={{ overflowWrap: 'anywhere' }}>
                    {field.value(company)}
                  </Typography>
                </Box>
              ))}
            </Box>
          )}
          {tab === 'employees' && (
            <ManagementCompanyEmployees managementCompanyId={company.id} />
          )}
          {tab === 'categories' && (
            <EmptyState
              title="Categorias em breve"
              description="O gerenciamento visual das categorias da administradora será implementado no próximo lote."
            />
          )}
        </CardContent>
      </Card>

      <ManagementCompanyFormDialog
        open={editOpen}
        company={company}
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
        <DialogTitle>{company.isActive ? 'Inativar administradora' : 'Ativar administradora'}</DialogTitle>
        <DialogContent>
          {operationError && <Alert severity="error" sx={{ mb: 2 }}>{operationError}</Alert>}
          <Typography>
            {company.isActive
              ? `A administradora ${company.name} ficará inativa. Seus condomínios e funcionários permanecerão vinculados.`
              : `A administradora ${company.name} voltará a ficar ativa.`}
          </Typography>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setStatusOpen(false)} disabled={isSaving}>Cancelar</Button>
          <Button
            variant="contained"
            color={company.isActive ? 'error' : 'primary'}
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
