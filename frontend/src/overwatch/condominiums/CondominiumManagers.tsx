import { useCallback, useEffect, useMemo, useState } from 'react'
import AddRoundedIcon from '@mui/icons-material/AddRounded'
import DeleteOutlineRoundedIcon from '@mui/icons-material/DeleteOutlineRounded'
import SwapHorizRoundedIcon from '@mui/icons-material/SwapHorizRounded'
import {
  Alert, Box, Button, Card, CardContent, Chip, CircularProgress, Dialog,
  DialogActions, DialogContent, DialogTitle, List, ListItemButton,
  ListItemText, Skeleton, Stack, TextField, Typography,
} from '@mui/material'
import { useNavigate } from 'react-router-dom'
import { EmptyState } from '../../components/EmptyState'
import { TransientFeedback } from '../../components/TransientFeedback'
import {
  getCondominiumManager,
  linkManager,
  listManagers,
  removeManagerLink,
  replaceCondominiumManager,
} from '../managers/api'
import { managerError } from '../managers/errors'
import type { CondominiumManager, OverwatchManager } from '../managers/types'
import {
  condominiumManagerCopy,
  eligibleManagers as filterEligibleManagers,
} from './managerPresentation'

type DialogMode = 'link' | 'replace' | null

export function CondominiumManagers({
  condominiumId,
  onChanged,
}: {
  condominiumId: string
  onChanged?: () => void
}) {
  const navigate = useNavigate()
  const [manager, setManager] = useState<CondominiumManager | null>(null)
  const [options, setOptions] = useState<OverwatchManager[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [isSaving, setIsSaving] = useState(false)
  const [error, setError] = useState('')
  const [mode, setMode] = useState<DialogMode>(null)
  const [removeOpen, setRemoveOpen] = useState(false)
  const [selectedId, setSelectedId] = useState('')
  const [search, setSearch] = useState('')
  const [feedback, setFeedback] = useState('')

  const load = useCallback(async () => {
    setIsLoading(true)
    setError('')
    try {
      const [linkedManager, managers] = await Promise.all([
        getCondominiumManager(condominiumId),
        listManagers(),
      ])
      setManager(linkedManager)
      setOptions(managers)
    } catch (requestError) {
      setError(managerError(requestError))
    } finally {
      setIsLoading(false)
    }
  }, [condominiumId])

  useEffect(() => {
    void load()
  }, [load])

  const eligibleManagers = useMemo(
    () => filterEligibleManagers(options, manager, search),
    [manager, options, search],
  )

  const openSelection = (nextMode: Exclude<DialogMode, null>) => {
    setSelectedId('')
    setSearch('')
    setError('')
    setMode(nextMode)
  }

  const save = async () => {
    if (!selectedId || !mode || isSaving) return
    setIsSaving(true)
    setError('')
    try {
      const saved = mode === 'replace'
        ? await replaceCondominiumManager(condominiumId, selectedId)
        : await linkManager(selectedId, condominiumId)
      setManager(saved)
      setMode(null)
      setFeedback(mode === 'replace'
        ? 'Síndico trocado com sucesso.'
        : 'Síndico vinculado com sucesso.')
      onChanged?.()
    } catch (requestError) {
      setError(managerError(requestError))
    } finally {
      setIsSaving(false)
    }
  }

  const remove = async () => {
    if (!manager || isSaving) return
    setIsSaving(true)
    setError('')
    try {
      await removeManagerLink(manager.userId, condominiumId)
      setManager(null)
      setRemoveOpen(false)
      setFeedback('Síndico desvinculado com sucesso.')
      onChanged?.()
    } catch (requestError) {
      setError(managerError(requestError))
    } finally {
      setIsSaving(false)
    }
  }

  if (isLoading) {
    return <Skeleton variant="rounded" height={220} />
  }

  return (
    <Box>
      <Typography variant="h2">{condominiumManagerCopy.sectionTitle}</Typography>
      <Typography color="text.secondary" mt={0.5}>
        Cada condomínio pode possuir no máximo um síndico ativo.
      </Typography>

      {error && !mode && !removeOpen && (
        <Alert severity="error" sx={{ mt: 2 }}
          action={<Button color="inherit" onClick={() => void load()}>
            Tentar novamente
          </Button>}>
          {error}
        </Alert>
      )}

      {!manager ? (
        <Box mt={3}>
          <EmptyState
            title={condominiumManagerCopy.emptyTitle}
            description="Selecione um síndico ativo para criar o vínculo administrativo."
            actionLabel={condominiumManagerCopy.linkAction}
            onAction={() => openSelection('link')}
          />
        </Box>
      ) : (
        <Card variant="outlined" sx={{ mt: 3 }}>
          <CardContent>
            <Stack
              direction={{ xs: 'column', sm: 'row' }}
              justifyContent="space-between"
              alignItems={{ sm: 'flex-start' }}
              gap={2}
            >
              <Box sx={{ minWidth: 0 }}>
                <Button
                  variant="text"
                  sx={{ p: 0, fontSize: '1.1rem', fontWeight: 750,
                    textTransform: 'none', overflowWrap: 'anywhere' }}
                  onClick={() => navigate(`/overwatch/managers/${manager.userId}`)}
                >
                  {manager.fullName}
                </Button>
                <Typography mt={1} sx={{ overflowWrap: 'anywhere' }}>
                  {manager.phoneNumber || 'Telefone / WhatsApp não informado'}
                </Typography>
                <Typography color="text.secondary" sx={{ overflowWrap: 'anywhere' }}>
                  {manager.email}
                </Typography>
                <Chip
                  size="small"
                  color={manager.isActive ? 'success' : 'default'}
                  label={manager.isActive ? 'Ativo' : 'Inativo'}
                  sx={{ mt: 1.5 }}
                />
              </Box>
              <Stack direction="row" gap={1} flexWrap="wrap">
                <Button
                  variant="outlined"
                  startIcon={<SwapHorizRoundedIcon />}
                  onClick={() => openSelection('replace')}
                >
                  {condominiumManagerCopy.replaceAction}
                </Button>
                <Button
                  color="error"
                  startIcon={<DeleteOutlineRoundedIcon />}
                  onClick={() => {
                    setError('')
                    setRemoveOpen(true)
                  }}
                >
                  {condominiumManagerCopy.unlinkAction}
                </Button>
              </Stack>
            </Stack>
          </CardContent>
        </Card>
      )}

      <Dialog
        open={Boolean(mode)}
        onClose={() => !isSaving && setMode(null)}
        fullWidth
        maxWidth="sm"
      >
        <DialogTitle>
          {mode === 'replace' ? 'Trocar síndico' : 'Vincular síndico'}
        </DialogTitle>
        <DialogContent>
          {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
          {mode === 'replace' && manager && (
            <Alert severity="warning" sx={{ mb: 2 }}>
              O síndico atual, {manager.fullName}, deixará de administrar este
              condomínio. Os vínculos dele com outros condomínios e seus outros
              papéis serão preservados.
            </Alert>
          )}
          <TextField
            autoFocus
            fullWidth
            label="Buscar por nome, e-mail ou telefone"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
          />
          <List sx={{ mt: 1, maxHeight: 320, overflow: 'auto' }}>
            {eligibleManagers.map((option) => (
              <ListItemButton
                key={option.id}
                selected={selectedId === option.id}
                onClick={() => setSelectedId(option.id)}
              >
                <ListItemText
                  primary={option.fullName}
                  secondary={`${option.email}${option.phoneNumber
                    ? ` • ${option.phoneNumber}`
                    : ''}`}
                />
              </ListItemButton>
            ))}
          </List>
          {!eligibleManagers.length && (
            <Stack alignItems="center" py={3} gap={1}>
              <Typography color="text.secondary" textAlign="center">
                Nenhum síndico ativo está disponível.
              </Typography>
              <Button
                startIcon={<AddRoundedIcon />}
                onClick={() => navigate('/overwatch/managers')}
              >
                Ir para cadastro de síndicos
              </Button>
            </Stack>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setMode(null)} disabled={isSaving}>
            Cancelar
          </Button>
          <Button
            variant="contained"
            disabled={!selectedId || isSaving}
            onClick={() => void save()}
          >
            {isSaving
              ? <CircularProgress size={20} color="inherit" />
              : mode === 'replace' ? 'Confirmar troca' : 'Vincular'}
          </Button>
        </DialogActions>
      </Dialog>

      <Dialog
        open={removeOpen}
        onClose={() => !isSaving && setRemoveOpen(false)}
        fullWidth
        maxWidth="xs"
      >
        <DialogTitle>Desvincular síndico</DialogTitle>
        <DialogContent>
          {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
          <Typography>
            Deseja desvincular {manager?.fullName} deste condomínio? O usuário,
            seus demais vínculos e outros papéis serão preservados.
          </Typography>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setRemoveOpen(false)} disabled={isSaving}>
            Cancelar
          </Button>
          <Button
            variant="contained"
            color="error"
            disabled={isSaving}
            onClick={() => void remove()}
          >
            {isSaving
              ? <CircularProgress size={20} color="inherit" />
              : 'Desvincular'}
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
