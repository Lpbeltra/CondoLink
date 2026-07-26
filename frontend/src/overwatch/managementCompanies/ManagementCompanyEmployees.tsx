import { useCallback, useEffect, useState, type FormEvent } from 'react'
import AddRoundedIcon from '@mui/icons-material/AddRounded'
import ContentCopyRoundedIcon from '@mui/icons-material/ContentCopyRounded'
import DeleteOutlineRoundedIcon from '@mui/icons-material/DeleteOutlineRounded'
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
  TextField,
  Tooltip,
  Typography,
} from '@mui/material'
import { EmptyState } from '../../components/EmptyState'
import { TransientFeedback } from '../../components/TransientFeedback'
import { formatDateTime } from '../../requests/presentation'
import {
  createManagementCompanyEmployee,
  listManagementCompanyEmployees,
  removeManagementCompanyEmployee,
  updateManagementCompanyEmployeeStatus,
} from './api'
import { employeeCredentialsText } from './credentials'
import { employeeError } from './errors'
import type {
  CreatedManagementCompanyEmployee,
  ManagementCompanyEmployee,
} from './types'
import { validateEmployee } from './validation'

interface Props {
  managementCompanyId: string
}

type PendingAction = {
  type: 'status' | 'remove'
  employee: ManagementCompanyEmployee
} | null

export function ManagementCompanyEmployees({ managementCompanyId }: Props) {
  const [employees, setEmployees] = useState<ManagementCompanyEmployee[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [loadError, setLoadError] = useState('')
  const [formOpen, setFormOpen] = useState(false)
  const [fullName, setFullName] = useState('')
  const [email, setEmail] = useState('')
  const [formError, setFormError] = useState('')
  const [isSaving, setIsSaving] = useState(false)
  const [credentials, setCredentials] =
    useState<CreatedManagementCompanyEmployee | null>(null)
  const [pendingAction, setPendingAction] = useState<PendingAction>(null)
  const [feedback, setFeedback] = useState('')

  const load = useCallback(async () => {
    setIsLoading(true)
    setLoadError('')
    try {
      setEmployees(await listManagementCompanyEmployees(managementCompanyId))
    } catch (error) {
      setLoadError(employeeError(error))
    } finally {
      setIsLoading(false)
    }
  }, [managementCompanyId])

  useEffect(() => {
    void load()
  }, [load])

  const openForm = () => {
    setFullName('')
    setEmail('')
    setFormError('')
    setFormOpen(true)
  }

  const createEmployee = async (event: FormEvent) => {
    event.preventDefault()
    if (isSaving) return

    const input = { fullName: fullName.trim(), email: email.trim() }
    const validationError = validateEmployee(input)
    if (validationError) {
      setFormError(validationError)
      return
    }

    setIsSaving(true)
    setFormError('')
    try {
      const created = await createManagementCompanyEmployee(
        managementCompanyId,
        input,
      )
      setFormOpen(false)
      setFullName('')
      setEmail('')
      setCredentials(created)
      await load()
    } catch (error) {
      setFormError(employeeError(error))
    } finally {
      setIsSaving(false)
    }
  }

  const closeCredentials = () => {
    setCredentials(null)
  }

  const copy = async (value: string, message: string) => {
    try {
      await navigator.clipboard.writeText(value)
      setFeedback(message)
    } catch {
      setFeedback('Não foi possível copiar. Selecione o conteúdo manualmente.')
    }
  }

  const confirmAction = async () => {
    if (!pendingAction || isSaving) return
    setIsSaving(true)
    setLoadError('')

    try {
      if (pendingAction.type === 'remove') {
        await removeManagementCompanyEmployee(pendingAction.employee.id)
        setEmployees((current) =>
          current.filter((item) => item.id !== pendingAction.employee.id))
        setFeedback('Vínculo do funcionário removido.')
      } else {
        const isActive = !pendingAction.employee.isActive
        const updated = await updateManagementCompanyEmployeeStatus(
          pendingAction.employee.id,
          isActive,
        )
        setEmployees((current) => current.map((item) =>
          item.id === pendingAction.employee.id
            ? { ...item, isActive: updated.isActive, updatedAt: updated.updatedAt }
            : item))
        setFeedback(
          isActive ? 'Funcionário ativado.' : 'Funcionário inativado.',
        )
      }
      setPendingAction(null)
    } catch (error) {
      setLoadError(employeeError(error))
      setPendingAction(null)
    } finally {
      setIsSaving(false)
    }
  }

  return (
    <Box>
      <Stack
        direction={{ xs: 'column', sm: 'row' }}
        justifyContent="space-between"
        alignItems={{ sm: 'center' }}
        gap={2}
      >
        <Box>
          <Typography variant="h2">Funcionários</Typography>
          <Typography color="text.secondary" mt={0.5}>
            Gerencie quem possui acesso operacional à administradora.
          </Typography>
        </Box>
        <Button variant="contained" startIcon={<AddRoundedIcon />} onClick={openForm}>
          Novo funcionário
        </Button>
      </Stack>

      {loadError && (
        <Alert
          severity="error"
          sx={{ mt: 2 }}
          action={<Button color="inherit" onClick={() => void load()}>Tentar novamente</Button>}
        >
          {loadError}
        </Alert>
      )}

      {isLoading ? (
        <Skeleton variant="rounded" height={240} sx={{ mt: 3 }} />
      ) : employees.length === 0 ? (
        <Box mt={3}>
          <EmptyState
            title="Nenhum funcionário cadastrado"
            description="Cadastre o primeiro funcionário para conceder acesso à administradora."
            actionLabel="Novo funcionário"
            onAction={openForm}
          />
        </Box>
      ) : (
        <TableContainer
          component={Paper}
          elevation={0}
          sx={{ mt: 3, border: '1px solid', borderColor: 'divider' }}
        >
          <Table sx={{ minWidth: 760 }}>
            <TableHead>
              <TableRow>
                {['Nome', 'E-mail', 'Status', 'Criado em', 'Ações'].map((column) => (
                  <TableCell key={column} sx={{ fontWeight: 750 }}>{column}</TableCell>
                ))}
              </TableRow>
            </TableHead>
            <TableBody>
              {employees.map((employee) => (
                <TableRow key={employee.id} hover>
                  <TableCell sx={{ fontWeight: 700 }}>{employee.fullName}</TableCell>
                  <TableCell>{employee.email}</TableCell>
                  <TableCell>
                    <Chip
                      size="small"
                      color={employee.isActive ? 'success' : 'default'}
                      label={employee.isActive ? 'Ativo' : 'Inativo'}
                    />
                  </TableCell>
                  <TableCell>{formatDateTime(employee.createdAt)}</TableCell>
                  <TableCell>
                    <Tooltip title={employee.isActive ? 'Inativar' : 'Ativar'}>
                      <IconButton
                        aria-label={employee.isActive ? 'Inativar funcionário' : 'Ativar funcionário'}
                        onClick={() => setPendingAction({ type: 'status', employee })}
                      >
                        <PowerSettingsNewRoundedIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                    <Tooltip title="Remover vínculo">
                      <IconButton
                        color="error"
                        aria-label="Remover vínculo do funcionário"
                        onClick={() => setPendingAction({ type: 'remove', employee })}
                      >
                        <DeleteOutlineRoundedIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      <Dialog
        open={formOpen}
        onClose={() => undefined}
        disableEscapeKeyDown
        fullWidth
        maxWidth="sm"
      >
        <Box component="form" onSubmit={(event) => void createEmployee(event)}>
          <DialogTitle>Novo funcionário</DialogTitle>
          <DialogContent>
            <Stack gap={2} pt={1}>
              {formError && <Alert severity="error">{formError}</Alert>}
              <TextField
                autoFocus
                required
                label="Nome completo"
                value={fullName}
                onChange={(event) => setFullName(event.target.value)}
                slotProps={{ htmlInput: { maxLength: 200 } }}
              />
              <TextField
                required
                type="email"
                label="E-mail"
                value={email}
                onChange={(event) => setEmail(event.target.value)}
                slotProps={{ htmlInput: { maxLength: 254 } }}
              />
            </Stack>
          </DialogContent>
          <DialogActions>
            <Button onClick={() => setFormOpen(false)} disabled={isSaving}>
              Cancelar
            </Button>
            <Button
              type="submit"
              variant="contained"
              disabled={isSaving || !fullName.trim() || !email.trim()}
            >
              {isSaving ? <CircularProgress size={20} color="inherit" /> : 'Criar funcionário'}
            </Button>
          </DialogActions>
        </Box>
      </Dialog>

      <Dialog
        open={Boolean(credentials)}
        onClose={() => undefined}
        disableEscapeKeyDown
        fullWidth
        maxWidth="sm"
      >
        <DialogTitle>Funcionário criado com sucesso</DialogTitle>
        <DialogContent>
          <Alert severity="warning">
            A senha temporária será exibida somente neste momento. Compartilhe-a de forma segura.
          </Alert>
          {credentials && (
            <Card variant="outlined" sx={{ mt: 2 }}>
              <CardContent>
                <Typography fontWeight={800}>{credentials.fullName}</Typography>
                <Typography mt={1}>E-mail: {credentials.email}</Typography>
                <Typography sx={{ fontFamily: 'monospace', mt: 1 }}>
                  Senha temporária: {credentials.temporaryPassword}
                </Typography>
                <Stack direction={{ xs: 'column', sm: 'row' }} gap={1} mt={2}>
                  <Button
                    size="small"
                    startIcon={<ContentCopyRoundedIcon />}
                    onClick={() => void copy(credentials.email, 'E-mail copiado.')}
                  >
                    Copiar e-mail
                  </Button>
                  <Button
                    size="small"
                    startIcon={<ContentCopyRoundedIcon />}
                    onClick={() => void copy(credentials.temporaryPassword, 'Senha copiada.')}
                  >
                    Copiar senha
                  </Button>
                </Stack>
              </CardContent>
            </Card>
          )}
        </DialogContent>
        <DialogActions>
          {credentials && (
            <Button
              startIcon={<ContentCopyRoundedIcon />}
              onClick={() => void copy(
                employeeCredentialsText(credentials),
                'Credenciais copiadas.',
              )}
            >
              Copiar credenciais completas
            </Button>
          )}
          <Button variant="contained" onClick={closeCredentials}>Concluir</Button>
        </DialogActions>
      </Dialog>

      <Dialog
        open={Boolean(pendingAction)}
        onClose={() => !isSaving && setPendingAction(null)}
        fullWidth
        maxWidth="xs"
      >
        <DialogTitle>
          {pendingAction?.type === 'remove'
            ? 'Remover vínculo'
            : pendingAction?.employee.isActive
              ? 'Inativar funcionário'
              : 'Ativar funcionário'}
        </DialogTitle>
        <DialogContent>
          {pendingAction?.type === 'remove' ? (
            <Alert severity="warning">
              O acesso de {pendingAction.employee.fullName} à administradora será removido.
              O usuário do CondoLink não será excluído.
            </Alert>
          ) : (
            <Typography>
              {pendingAction?.employee.isActive
                ? 'O acesso operacional à administradora será inativado. O usuário continuará existindo.'
                : 'O acesso operacional à administradora será reativado.'}
            </Typography>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setPendingAction(null)} disabled={isSaving}>Cancelar</Button>
          <Button
            variant="contained"
            color={pendingAction?.type === 'remove' ? 'error' : 'primary'}
            disabled={isSaving}
            onClick={() => void confirmAction()}
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
    </Box>
  )
}
