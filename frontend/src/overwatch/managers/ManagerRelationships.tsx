import { useCallback, useEffect, useMemo, useState } from 'react'
import AddRoundedIcon from '@mui/icons-material/AddRounded'
import DeleteOutlineRoundedIcon from '@mui/icons-material/DeleteOutlineRounded'
import {
  Alert, Box, Button, Chip, CircularProgress, Dialog, DialogActions,
  DialogContent, DialogTitle, List, ListItemButton, ListItemText, Paper,
  Skeleton, Stack, Table, TableBody, TableCell, TableContainer, TableHead,
  TableRow, TextField, Typography,
} from '@mui/material'
import { EmptyState } from '../../components/EmptyState'
import { TransientFeedback } from '../../components/TransientFeedback'
import { formatDateTime } from '../../requests/presentation'
import {
  linkManager,
  listAvailableCondominiums,
  listCondominiumManagers,
  listManagerCondominiums,
  listManagers,
  removeManagerLink,
} from './api'
import { managerError } from './errors'
import type {
  CondominiumManager,
  ManagerCondominium,
  OverwatchManager,
} from './types'
import type { OverwatchCondominium } from '../condominiums/types'

type Props =
  | { managerId: string; condominiumId?: never; onChanged?: () => void }
  | { condominiumId: string; managerId?: never; onChanged?: () => void }

interface Option {
  id: string
  primary: string
  secondary: string
}

export function ManagerRelationships(props: Props) {
  const managerPerspective = Boolean(props.managerId)
  const [managerLinks, setManagerLinks] = useState<ManagerCondominium[]>([])
  const [condominiumLinks, setCondominiumLinks] = useState<CondominiumManager[]>([])
  const [managerOptions, setManagerOptions] = useState<OverwatchManager[]>([])
  const [condominiumOptions, setCondominiumOptions] = useState<OverwatchCondominium[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [isSaving, setIsSaving] = useState(false)
  const [error, setError] = useState('')
  const [dialogOpen, setDialogOpen] = useState(false)
  const [pendingRemove, setPendingRemove] = useState<Option | null>(null)
  const [selectedId, setSelectedId] = useState('')
  const [search, setSearch] = useState('')
  const [feedback, setFeedback] = useState('')

  const load = useCallback(async () => {
    setIsLoading(true)
    setError('')
    try {
      if (props.managerId) {
        const [links, condominiums] = await Promise.all([
          listManagerCondominiums(props.managerId),
          listAvailableCondominiums(),
        ])
        setManagerLinks(links)
        setCondominiumOptions(condominiums)
      } else {
        const [links, managers] = await Promise.all([
          listCondominiumManagers(props.condominiumId!),
          listManagers(),
        ])
        setCondominiumLinks(links)
        setManagerOptions(managers)
      }
    } catch (requestError) {
      setError(managerError(requestError))
    } finally {
      setIsLoading(false)
    }
  }, [props.condominiumId, props.managerId])

  useEffect(() => {
    void load()
  }, [load])

  const options = useMemo<Option[]>(() => {
    const term = search.trim().toLocaleLowerCase()
    if (managerPerspective) {
      const linked = new Set(managerLinks.map((item) => item.condominiumId))
      return condominiumOptions
        .filter((item) => item.isActive && !linked.has(item.id))
        .filter((item) => item.name.toLocaleLowerCase().includes(term))
        .map((item) => ({
          id: item.id,
          primary: item.name,
          secondary: item.managementCompanyName || 'Sem administradora',
        }))
    }
    const linked = new Set(condominiumLinks.map((item) => item.userId))
    return managerOptions
      .filter((item) => item.isActive && !linked.has(item.id))
      .filter((item) =>
        `${item.fullName} ${item.email}`.toLocaleLowerCase().includes(term))
      .map((item) => ({ id: item.id, primary: item.fullName, secondary: item.email }))
  }, [
    condominiumLinks, condominiumOptions, managerLinks,
    managerOptions, managerPerspective, search,
  ])

  const openLink = () => {
    setSelectedId('')
    setSearch('')
    setError('')
    setDialogOpen(true)
  }

  const add = async () => {
    if (!selectedId || isSaving) return
    const managerId = props.managerId || selectedId
    const condominiumId = props.condominiumId || selectedId
    setIsSaving(true)
    setError('')
    try {
      await linkManager(managerId, condominiumId)
      setDialogOpen(false)
      setFeedback('Vínculo criado.')
      await load()
      props.onChanged?.()
    } catch (requestError) {
      setError(managerError(requestError))
    } finally {
      setIsSaving(false)
    }
  }

  const remove = async () => {
    if (!pendingRemove || isSaving) return
    const managerId = props.managerId || pendingRemove.id
    const condominiumId = props.condominiumId || pendingRemove.id
    setIsSaving(true)
    setError('')
    try {
      await removeManagerLink(managerId, condominiumId)
      setPendingRemove(null)
      setFeedback('Vínculo removido.')
      await load()
      props.onChanged?.()
    } catch (requestError) {
      setError(managerError(requestError))
    } finally {
      setIsSaving(false)
    }
  }

  if (isLoading) return <Skeleton variant="rounded" height={220} />

  const hasLinks = managerPerspective
    ? managerLinks.length > 0
    : condominiumLinks.length > 0

  return (
    <Box>
      <Stack direction={{ xs: 'column', sm: 'row' }} justifyContent="space-between" gap={2}>
        <Box>
          <Typography variant="h2">
            {managerPerspective ? 'Condomínios vinculados' : 'Síndicos vinculados'}
          </Typography>
          <Typography color="text.secondary" mt={0.5}>
            Gerencie os acessos administrativos deste vínculo.
          </Typography>
        </Box>
        <Button variant="contained" startIcon={<AddRoundedIcon />} onClick={openLink}>
          {managerPerspective ? 'Vincular condomínio' : 'Vincular síndico'}
        </Button>
      </Stack>
      {error && (
        <Alert severity="error" sx={{ mt: 2 }}
          action={<Button color="inherit" onClick={() => void load()}>Tentar novamente</Button>}>
          {error}
        </Alert>
      )}
      {!hasLinks ? (
        <Box mt={3}>
          <EmptyState
            title={managerPerspective
              ? 'Nenhum condomínio vinculado'
              : 'Nenhum síndico vinculado'}
            description="Crie o primeiro vínculo administrativo."
            actionLabel={managerPerspective ? 'Vincular condomínio' : 'Vincular síndico'}
            onAction={openLink}
          />
        </Box>
      ) : (
        <TableContainer component={Paper} elevation={0}
          sx={{ mt: 3, border: '1px solid', borderColor: 'divider' }}>
          <Table sx={{ minWidth: 720 }}>
            <TableHead>
              <TableRow>
                {(managerPerspective
                  ? ['Nome', 'Administradora', 'Status', 'Vinculado em', 'Ações']
                  : ['Nome', 'E-mail', 'Status', 'Vinculado em', 'Ações'])
                  .map((column) => (
                    <TableCell key={column} sx={{ fontWeight: 750 }}>{column}</TableCell>
                  ))}
              </TableRow>
            </TableHead>
            <TableBody>
              {managerPerspective
                ? managerLinks.map((link) => (
                    <TableRow key={link.membershipId}>
                      <TableCell sx={{ fontWeight: 700 }}>{link.name}</TableCell>
                      <TableCell>{link.managementCompanyName || 'Sem administradora'}</TableCell>
                      <TableCell>
                        <Chip size="small" color={link.isActive ? 'success' : 'default'}
                          label={link.isActive ? 'Ativo' : 'Inativo'} />
                      </TableCell>
                      <TableCell>{formatDateTime(link.joinedAt)}</TableCell>
                      <TableCell>
                        <Button color="error" startIcon={<DeleteOutlineRoundedIcon />}
                          onClick={() => setPendingRemove({
                            id: link.condominiumId,
                            primary: link.name,
                            secondary: '',
                          })}>
                          Remover vínculo
                        </Button>
                      </TableCell>
                    </TableRow>
                  ))
                : condominiumLinks.map((link) => (
                    <TableRow key={link.membershipId}>
                      <TableCell sx={{ fontWeight: 700 }}>{link.fullName}</TableCell>
                      <TableCell>{link.email}</TableCell>
                      <TableCell>
                        <Chip size="small" color={link.isActive ? 'success' : 'default'}
                          label={link.isActive ? 'Ativo' : 'Usuário inativo'} />
                      </TableCell>
                      <TableCell>{formatDateTime(link.joinedAt)}</TableCell>
                      <TableCell>
                        <Button color="error" startIcon={<DeleteOutlineRoundedIcon />}
                          onClick={() => setPendingRemove({
                            id: link.userId,
                            primary: link.fullName,
                            secondary: link.email,
                          })}>
                          Remover vínculo
                        </Button>
                      </TableCell>
                    </TableRow>
                  ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      <Dialog open={dialogOpen} onClose={() => !isSaving && setDialogOpen(false)}
        fullWidth maxWidth="sm">
        <DialogTitle>
          {managerPerspective ? 'Vincular condomínio' : 'Vincular síndico'}
        </DialogTitle>
        <DialogContent>
          {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
          <TextField
            autoFocus fullWidth label="Buscar por nome" value={search}
            onChange={(event) => setSearch(event.target.value)}
          />
          <List sx={{ mt: 1, maxHeight: 320, overflow: 'auto' }}>
            {options.map((option) => (
              <ListItemButton key={option.id} selected={selectedId === option.id}
                onClick={() => setSelectedId(option.id)}>
                <ListItemText primary={option.primary} secondary={option.secondary} />
              </ListItemButton>
            ))}
          </List>
          {!options.length && (
            <Typography color="text.secondary" textAlign="center" py={3}>
              Nenhuma opção disponível.
            </Typography>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDialogOpen(false)} disabled={isSaving}>Cancelar</Button>
          <Button variant="contained" disabled={!selectedId || isSaving}
            onClick={() => void add()}>
            {isSaving ? <CircularProgress size={20} color="inherit" /> : 'Vincular'}
          </Button>
        </DialogActions>
      </Dialog>

      <Dialog open={Boolean(pendingRemove)}
        onClose={() => !isSaving && setPendingRemove(null)} fullWidth maxWidth="xs">
        <DialogTitle>Remover vínculo</DialogTitle>
        <DialogContent>
          <Alert severity="warning">
            Este síndico perderá o acesso administrativo a este condomínio.
            O usuário continuará existindo e outros vínculos não serão alterados.
          </Alert>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setPendingRemove(null)} disabled={isSaving}>Cancelar</Button>
          <Button variant="contained" color="error" disabled={isSaving}
            onClick={() => void remove()}>
            {isSaving ? <CircularProgress size={20} color="inherit" /> : 'Remover vínculo'}
          </Button>
        </DialogActions>
      </Dialog>
      <TransientFeedback message={feedback} severity="success"
        onClose={() => setFeedback('')} />
    </Box>
  )
}
