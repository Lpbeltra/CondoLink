import { useCallback, useEffect, useState } from 'react'
import AddRoundedIcon from '@mui/icons-material/AddRounded'
import OpenInNewRoundedIcon from '@mui/icons-material/OpenInNewRounded'
import PowerSettingsNewRoundedIcon from '@mui/icons-material/PowerSettingsNewRounded'
import {
  Alert, Box, Button, Chip, CircularProgress, Dialog, DialogActions,
  DialogContent, DialogTitle, Paper, Skeleton, Stack, Table, TableBody,
  TableCell, TableContainer, TableHead, TableRow, Typography,
} from '@mui/material'
import { useNavigate } from 'react-router-dom'
import { EmptyState } from '../../components/EmptyState'
import { PageContainer } from '../../components/PageContainer'
import { TransientFeedback } from '../../components/TransientFeedback'
import { formatDateTime } from '../../requests/presentation'
import { createManager, listManagers, updateManagerStatus } from '../managers/api'
import { managerError } from '../managers/errors'
import { ManagerCredentialsDialog } from '../managers/ManagerCredentialsDialog'
import { ManagerFormDialog } from '../managers/ManagerFormDialog'
import { managerDetailsPath, upsertManager } from '../managers/presentation'
import type {
  CreatedManager, ManagerInput, OverwatchManager,
} from '../managers/types'

export function OverwatchManagersPage() {
  const navigate = useNavigate()
  const [managers, setManagers] = useState<OverwatchManager[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState('')
  const [formOpen, setFormOpen] = useState(false)
  const [formError, setFormError] = useState('')
  const [isSaving, setIsSaving] = useState(false)
  const [credentials, setCredentials] = useState<CreatedManager | null>(null)
  const [statusManager, setStatusManager] = useState<OverwatchManager | null>(null)
  const [feedback, setFeedback] = useState('')

  const load = useCallback(async () => {
    setIsLoading(true)
    setError('')
    try {
      setManagers(await listManagers())
    } catch (requestError) {
      setError(managerError(requestError))
    } finally {
      setIsLoading(false)
    }
  }, [])

  useEffect(() => { void load() }, [load])

  const create = async (input: ManagerInput) => {
    if (isSaving) return
    setIsSaving(true)
    setFormError('')
    try {
      const created = await createManager(input)
      setManagers((current) => upsertManager(current, created))
      setFormOpen(false)
      setCredentials(created)
    } catch (requestError) {
      setFormError(managerError(requestError))
    } finally {
      setIsSaving(false)
    }
  }

  const changeStatus = async () => {
    if (!statusManager || isSaving) return
    setIsSaving(true)
    setError('')
    try {
      const result = await updateManagerStatus(
        statusManager.id, !statusManager.isActive,
      )
      setManagers((current) => current.map((item) =>
        item.id === statusManager.id
          ? { ...item, isActive: result.isActive, updatedAt: result.updatedAt }
          : item))
      setFeedback(result.isActive ? 'Síndico ativado.' : 'Síndico inativado.')
      setStatusManager(null)
    } catch (requestError) {
      setError(managerError(requestError))
    } finally {
      setIsSaving(false)
    }
  }

  return (
    <PageContainer>
      <Stack direction={{ xs: 'column', sm: 'row' }} justifyContent="space-between" gap={2}>
        <Box>
          <Typography variant="h1">Síndicos</Typography>
          <Typography color="text.secondary" mt={1}>
            Consulte os síndicos cadastrados e seus vínculos.
          </Typography>
        </Box>
        <Button variant="contained" startIcon={<AddRoundedIcon />}
          onClick={() => { setFormError(''); setFormOpen(true) }}>
          Novo síndico
        </Button>
      </Stack>
      {error && (
        <Alert severity="error" sx={{ mt: 2 }}
          action={<Button color="inherit" onClick={() => void load()}>Tentar novamente</Button>}>
          {error}
        </Alert>
      )}
      {isLoading ? (
        <Skeleton variant="rounded" height={260} sx={{ mt: 3 }} />
      ) : managers.length === 0 ? (
        <Box mt={3}>
          <EmptyState title="Nenhum síndico cadastrado"
            description="Cadastre o primeiro síndico da plataforma."
            actionLabel="Novo síndico" onAction={() => setFormOpen(true)} />
        </Box>
      ) : (
        <TableContainer component={Paper} elevation={0}
          sx={{ mt: 3, border: '1px solid', borderColor: 'divider' }}>
          <Table sx={{ minWidth: 850 }}>
            <TableHead>
              <TableRow>
                {['Nome', 'E-mail', 'Status', 'Condomínios', 'Criado em', 'Ações']
                  .map((column) => (
                    <TableCell key={column} sx={{ fontWeight: 750 }}>{column}</TableCell>
                  ))}
              </TableRow>
            </TableHead>
            <TableBody>
              {managers.map((manager) => (
                <TableRow key={manager.id} hover>
                  <TableCell sx={{ fontWeight: 700 }}>{manager.fullName}</TableCell>
                  <TableCell>{manager.email}</TableCell>
                  <TableCell>
                    <Chip size="small" color={manager.isActive ? 'success' : 'default'}
                      label={manager.isActive ? 'Ativo' : 'Inativo'} />
                  </TableCell>
                  <TableCell>{manager.condominiumCount}</TableCell>
                  <TableCell>{formatDateTime(manager.createdAt)}</TableCell>
                  <TableCell>
                    <Stack direction="row" gap={1}>
                      <Button size="small" endIcon={<OpenInNewRoundedIcon />}
                        onClick={() => navigate(managerDetailsPath(manager.id))}>
                        Gerenciar
                      </Button>
                      <Button size="small" color={manager.isActive ? 'error' : 'primary'}
                        startIcon={<PowerSettingsNewRoundedIcon />}
                        onClick={() => setStatusManager(manager)}>
                        {manager.isActive ? 'Inativar' : 'Ativar'}
                      </Button>
                    </Stack>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}
      <ManagerFormDialog open={formOpen} isSaving={isSaving} error={formError}
        onClose={() => setFormOpen(false)} onSubmit={create} />
      <ManagerCredentialsDialog manager={credentials}
        onClose={() => setCredentials(null)} onCopied={setFeedback} />
      <Dialog open={Boolean(statusManager)}
        onClose={() => !isSaving && setStatusManager(null)} fullWidth maxWidth="xs">
        <DialogTitle>
          {statusManager?.isActive ? 'Inativar síndico' : 'Ativar síndico'}
        </DialogTitle>
        <DialogContent>
          <Typography>
            {statusManager?.isActive
              ? 'O usuário perderá o acesso ao sistema, mas seus vínculos com condomínios serão preservados.'
              : 'O usuário voltará a ter acesso ao sistema. Seus vínculos existentes serão mantidos.'}
          </Typography>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setStatusManager(null)} disabled={isSaving}>Cancelar</Button>
          <Button variant="contained" color={statusManager?.isActive ? 'error' : 'primary'}
            disabled={isSaving} onClick={() => void changeStatus()}>
            {isSaving ? <CircularProgress size={20} color="inherit" /> : 'Confirmar'}
          </Button>
        </DialogActions>
      </Dialog>
      <TransientFeedback message={feedback} severity="success"
        onClose={() => setFeedback('')} />
    </PageContainer>
  )
}
