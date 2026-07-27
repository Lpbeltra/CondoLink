import { useCallback, useEffect, useState } from 'react'
import AddRoundedIcon from '@mui/icons-material/AddRounded'
import EditRoundedIcon from '@mui/icons-material/EditRounded'
import OpenInNewRoundedIcon from '@mui/icons-material/OpenInNewRounded'
import PowerSettingsNewRoundedIcon from '@mui/icons-material/PowerSettingsNewRounded'
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  IconButton,
  Paper,
  Skeleton,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Tooltip,
  Typography,
} from '@mui/material'
import { useNavigate } from 'react-router-dom'
import { EmptyState } from '../../components/EmptyState'
import { PageContainer } from '../../components/PageContainer'
import { TransientFeedback } from '../../components/TransientFeedback'
import {
  createOverwatchCondominium,
  getOverwatchCondominium,
  listManagementCompanyOptions,
  listOverwatchCondominiums,
  setCondominiumManagementCompany,
  updateOverwatchCondominium,
  updateOverwatchCondominiumStatus,
} from '../condominiums/api'
import { condominiumError } from '../condominiums/errors'
import { CondominiumFormDialog } from '../condominiums/CondominiumFormDialog'
import {
  condominiumDetailsPath,
  upsertCondominium,
} from '../condominiums/presentation'
import type {
  CondominiumInput,
  ManagementCompanyOption,
  OverwatchCondominium,
} from '../condominiums/types'

interface SaveRequest {
  input: CondominiumInput
  managementCompanyId: string | null
}

export function OverwatchCondominiumsPage() {
  const navigate = useNavigate()
  const [condominiums, setCondominiums] = useState<OverwatchCondominium[]>([])
  const [managementCompanies, setManagementCompanies] =
    useState<ManagementCompanyOption[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState('')
  const [formCondominium, setFormCondominium] =
    useState<OverwatchCondominium | null | undefined>()
  const [statusCondominium, setStatusCondominium] =
    useState<OverwatchCondominium | null>(null)
  const [formError, setFormError] = useState('')
  const [isSaving, setIsSaving] = useState(false)
  const [feedback, setFeedback] = useState('')

  const load = useCallback(async () => {
    setIsLoading(true)
    setError('')
    try {
      const [condominiumData, companyData] = await Promise.all([
        listOverwatchCondominiums(),
        listManagementCompanyOptions(),
      ])
      setCondominiums(condominiumData)
      setManagementCompanies(companyData)
    } catch (requestError) {
      setError(condominiumError(requestError))
    } finally {
      setIsLoading(false)
    }
  }, [])

  useEffect(() => {
    void load()
  }, [load])

  const save = async ({ input, managementCompanyId }: SaveRequest) => {
    if (isSaving) return
    setIsSaving(true)
    setFormError('')
    try {
      let id: string
      if (formCondominium) {
        id = formCondominium.id
        await updateOverwatchCondominium(id, input)
      } else {
        id = (await createOverwatchCondominium(input)).id
      }

      if (
        !formCondominium
        ? managementCompanyId !== null
        : managementCompanyId !== formCondominium.managementCompanyId
      ) {
        await setCondominiumManagementCompany(id, managementCompanyId)
      }

      const saved = await getOverwatchCondominium(id)
      setCondominiums((current) => upsertCondominium(current, saved))
      setFormCondominium(undefined)
      setFeedback(formCondominium ? 'Condomínio atualizado.' : 'Condomínio criado.')
    } catch (requestError) {
      setFormError(condominiumError(requestError))
    } finally {
      setIsSaving(false)
    }
  }

  const changeStatus = async () => {
    if (!statusCondominium || isSaving) return
    setIsSaving(true)
    setError('')
    try {
      await updateOverwatchCondominiumStatus(
        statusCondominium.id,
        !statusCondominium.isActive,
      )
      const saved = await getOverwatchCondominium(statusCondominium.id)
      setCondominiums((current) => upsertCondominium(current, saved))
      setFeedback(saved.isActive ? 'Condomínio ativado.' : 'Condomínio inativado.')
      setStatusCondominium(null)
    } catch (requestError) {
      setError(condominiumError(requestError))
    } finally {
      setIsSaving(false)
    }
  }

  return (
    <PageContainer>
      <Stack direction={{ xs: 'column', sm: 'row' }} justifyContent="space-between" gap={2}>
        <Box>
          <Typography variant="h1">Condomínios</Typography>
          <Typography color="text.secondary" mt={1}>
            Consulte e administre os condomínios da plataforma.
          </Typography>
        </Box>
        <Button
          variant="contained"
          startIcon={<AddRoundedIcon />}
          onClick={() => {
            setFormError('')
            setFormCondominium(null)
          }}
        >
          Novo condomínio
        </Button>
      </Stack>

      {error && (
        <Alert
          severity="error"
          sx={{ mt: 2 }}
          action={<Button color="inherit" onClick={() => void load()}>Tentar novamente</Button>}
        >
          {error}
        </Alert>
      )}

      {isLoading ? (
        <Skeleton variant="rounded" height={280} sx={{ mt: 3 }} />
      ) : condominiums.length === 0 ? (
        <Box mt={3}>
          <EmptyState
            title="Nenhum condomínio cadastrado"
            description="Cadastre o primeiro condomínio da plataforma."
            actionLabel="Novo condomínio"
            onAction={() => setFormCondominium(null)}
          />
        </Box>
      ) : (
        <TableContainer
          component={Paper}
          elevation={0}
          sx={{ mt: 3, border: '1px solid', borderColor: 'divider' }}
        >
          <Table sx={{ minWidth: 700 }}>
            <TableHead>
              <TableRow>
                {['Nome', 'Administradora', 'Síndicos', 'Status e ações'].map((column) => (
                  <TableCell key={column} sx={{ fontWeight: 750 }}>{column}</TableCell>
                ))}
              </TableRow>
            </TableHead>
            <TableBody>
              {condominiums.map((condominium) => (
                <TableRow key={condominium.id} hover>
                  <TableCell sx={{ fontWeight: 750 }}>{condominium.name}</TableCell>
                  <TableCell>{condominium.managementCompanyName || 'Sem administradora'}</TableCell>
                  <TableCell>{condominium.managerCount}</TableCell>
                  <TableCell>
                    <Tooltip title="Editar">
                      <IconButton
                        aria-label={`Editar ${condominium.name}`}
                        onClick={() => {
                          setFormError('')
                          setFormCondominium(condominium)
                        }}
                      >
                        <EditRoundedIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                    <Button
                        size="small"
                        color={condominium.isActive ? 'error' : 'primary'}
                        startIcon={<PowerSettingsNewRoundedIcon />}
                        aria-label={`${condominium.isActive ? 'Inativar' : 'Ativar'} ${condominium.name}`}
                        onClick={() => setStatusCondominium(condominium)}
                      >
                        {condominium.isActive ? 'Inativar' : 'Ativar'}
                      </Button>
                    <Button
                      size="small"
                      endIcon={<OpenInNewRoundedIcon />}
                      onClick={() => navigate(condominiumDetailsPath(condominium.id))}
                    >
                      Gerenciar
                    </Button>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      <CondominiumFormDialog
        open={formCondominium !== undefined}
        condominium={formCondominium}
        managementCompanies={managementCompanies}
        isSaving={isSaving}
        error={formError}
        onClose={() => setFormCondominium(undefined)}
        onSubmit={save}
      />

      <Dialog
        open={Boolean(statusCondominium)}
        onClose={() => !isSaving && setStatusCondominium(null)}
        fullWidth
        maxWidth="xs"
      >
        <DialogTitle>
          {statusCondominium?.isActive ? 'Inativar condomínio' : 'Ativar condomínio'}
        </DialogTitle>
        <DialogContent>
          {statusCondominium?.isActive
            ? `O condomínio ${statusCondominium.name} ficará inativo. Seus vínculos serão preservados.`
            : `O condomínio ${statusCondominium?.name} voltará a ficar ativo.`}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setStatusCondominium(null)} disabled={isSaving}>Cancelar</Button>
          <Button
            variant="contained"
            color={statusCondominium?.isActive ? 'error' : 'primary'}
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
