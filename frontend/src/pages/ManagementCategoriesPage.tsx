import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react'
import AddRoundedIcon from '@mui/icons-material/AddRounded'
import MoreVertRoundedIcon from '@mui/icons-material/MoreVertRounded'
import SearchRoundedIcon from '@mui/icons-material/SearchRounded'
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
  InputAdornment,
  Menu,
  MenuItem,
  Skeleton,
  Stack,
  TextField,
  Typography,
} from '@mui/material'
import { useNavigate } from 'react-router-dom'
import { EmptyState } from '../components/EmptyState'
import { PageContainer } from '../components/PageContainer'
import { TransientFeedback } from '../components/TransientFeedback'
import {
  createCategory,
  deleteCategory,
  listCategories,
  updateCategory,
} from '../management/api'
import { filterCategories } from '../management/categoryPresentation'
import { managementError } from '../management/errors'
import { useManagementContext } from '../management/ManagementContext'
import type { Category } from '../management/types'

export function ManagementCategoriesPage() {
  const { activeCondominiumId } = useManagementContext()
  const navigate = useNavigate()

  const condominiumId = activeCondominiumId

  const [items, setItems] = useState<Category[]>([])
  const [search, setSearch] = useState('')
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')
  const [editing, setEditing] = useState<Category | null | undefined>(
    undefined,
  )
  const [deleting, setDeleting] = useState<Category | null>(null)
  const [actionTarget, setActionTarget] = useState<Category | null>(null)
  const [actionAnchor, setActionAnchor] = useState<HTMLElement | null>(null)
  const [name, setName] = useState('')
  const loadVersion = useRef(0)
  const activeIdRef = useRef(condominiumId)
  activeIdRef.current = condominiumId

  const load = useCallback(async () => {
    const version = ++loadVersion.current
    setItems([])
    setSearch('')
    setEditing(undefined)
    setDeleting(null)
    setSuccess('')
    setSaving(false)
    if (!condominiumId) {
      setItems([])
      setLoading(false)
      return
    }

    setLoading(true)
    setError('')

    try {
      const result = await listCategories(condominiumId)
      if (version === loadVersion.current) setItems(result)
    } catch (requestError) {
      if (version === loadVersion.current) setError(managementError(requestError))
    } finally {
      if (version === loadVersion.current) setLoading(false)
    }
  }, [condominiumId])

  useEffect(() => {
    void load()
  }, [load])

  const visible = filterCategories(items, search)

  const save = async (event: FormEvent) => {
    event.preventDefault()

    if (!condominiumId || !name.trim() || saving) return

    setSaving(true)
    setError('')

    const operationCondominiumId = condominiumId
    try {
      if (editing) {
        await updateCategory(condominiumId, editing.id, name.trim())
      } else {
        await createCategory(condominiumId, {
          name: name.trim(),
          description: null,
        })
      }

      if (activeIdRef.current !== operationCondominiumId) return

      setEditing(undefined)
      setName('')
      setSuccess(
        editing
          ? 'Categoria atualizada com sucesso.'
          : 'Categoria criada com sucesso.',
      )

      await load()
    } catch (requestError) {
      if (activeIdRef.current === operationCondominiumId) setError(managementError(requestError))
    } finally {
      if (activeIdRef.current === operationCondominiumId) setSaving(false)
    }
  }

  const remove = async () => {
    if (!condominiumId || !deleting || saving) return

    setSaving(true)
    setError('')

    const operationCondominiumId = condominiumId
    try {
      await deleteCategory(condominiumId, deleting.id)

      if (activeIdRef.current !== operationCondominiumId) return

      setDeleting(null)
      setSuccess('Categoria excluída com sucesso.')

      await load()
    } catch (requestError) {
      if (activeIdRef.current === operationCondominiumId) {
        setDeleting(null)
        setError(managementError(requestError))
      }
    } finally {
      if (activeIdRef.current === operationCondominiumId) setSaving(false)
    }
  }

  if (!activeCondominiumId && !loading) {
    return (
      <PageContainer>
        <Alert severity="info">
          Selecione um condomínio para consultar e cadastrar categorias.
        </Alert>
      </PageContainer>
    )
  }

  return (
    <PageContainer>
      <TransientFeedback
        message={success}
        severity="success"
        onClose={() => setSuccess('')}
      />

      <Stack
        direction={{ xs: 'column', sm: 'row' }}
        justifyContent="space-between"
        gap={2}
      >
        <Box>
          <Typography variant="h1">Categorias</Typography>
          <Typography color="text.secondary">
            Organize os atendimentos do condomínio por categoria.
          </Typography>
        </Box>

        <Button
          variant="contained"
          startIcon={<AddRoundedIcon />}
          onClick={() => {
            setEditing(null)
            setName('')
            setError('')
          }}
        >
          Adicionar categoria
        </Button>
      </Stack>

      {error && (
        <Alert severity="error" sx={{ mt: 2 }}>
          {error}
        </Alert>
      )}

      {loading ? (
        <Skeleton variant="rounded" height={180} sx={{ mt: 3 }} />
      ) : (
        <>
          <TextField
            size="small"
            label="Buscar categoria"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            sx={{
              mt: 3,
              width: {
                xs: '100%',
                sm: 360,
              },
            }}
            InputProps={{
              startAdornment: (
                <InputAdornment position="start">
                  <SearchRoundedIcon />
                </InputAdornment>
              ),
            }}
          />

          {items.length === 0 ? (
            <EmptyState
              title="Nenhuma categoria cadastrada."
              description="Adicione uma categoria para organizar os atendimentos."
              actionLabel="Adicionar categoria"
              onAction={() => { setEditing(null); setName(''); setError('') }}
            />
          ) : visible.length === 0 ? (
            <EmptyState
              title="Nenhuma categoria encontrada."
              description="Revise o texto pesquisado."
            />
          ) : (
            <Box role="list" sx={{ mt: 2, borderTop: '1px solid', borderColor: 'divider' }}>
              {visible.map((item) => (
                <Box
                  key={item.id}
                  role="listitem"
                  sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr auto', sm: 'minmax(220px, 1fr) 180px auto' }, gap: 1.5, alignItems: 'center', py: 1.25, px: { xs: 1, sm: 2 }, borderBottom: '1px solid', borderColor: 'divider', '@media (prefers-reduced-motion: no-preference)': { transition: 'background-color 150ms ease' }, '&:hover': { bgcolor: 'action.hover' } }}
                >
                  <Button
                    color="inherit"
                    onClick={() =>
                      navigate(
                        `/management/requests?categoryId=${item.id}`,
                      )
                    }
                    sx={{ justifyContent: 'flex-start', p: 0, minWidth: 0, textTransform: 'none', '&:hover': { bgcolor: 'transparent', textDecoration: 'underline' } }}
                    aria-label={`Abrir atendimento da categoria ${item.name}`}
                  >
                    <Typography fontWeight={750}>{item.name}</Typography>
                  </Button>
                  <Typography color="text.secondary" variant="body2" sx={{ display: { xs: 'none', sm: 'block' } }}>{item.requestCount === 0 ? 'Nenhum atendimento' : `${item.requestCount} ${item.requestCount === 1 ? 'atendimento' : 'atendimentos'}`}</Typography>
                  <IconButton aria-label={`Ações de ${item.name}`} onClick={(event) => { setActionAnchor(event.currentTarget); setActionTarget(item) }}><MoreVertRoundedIcon /></IconButton>
                  <Typography color="text.secondary" variant="body2" sx={{ display: { xs: 'block', sm: 'none' }, gridColumn: '1 / -1' }}>{item.requestCount === 0 ? 'Nenhum atendimento' : `${item.requestCount} ${item.requestCount === 1 ? 'atendimento' : 'atendimentos'}`}</Typography>
                </Box>
              ))}
            </Box>
          )}
        </>
      )}

      <Menu anchorEl={actionAnchor} open={Boolean(actionAnchor)} onClose={() => { setActionAnchor(null); setActionTarget(null) }}>
        {actionTarget && <MenuItem onClick={() => { setEditing(actionTarget); setName(actionTarget.name); setError(''); setActionAnchor(null); setActionTarget(null) }}>Editar</MenuItem>}
        {actionTarget && <MenuItem sx={{ color: 'error.main' }} onClick={() => { setDeleting(actionTarget); setActionAnchor(null); setActionTarget(null) }}>Excluir</MenuItem>}
      </Menu>

      <Dialog
        open={editing !== undefined}
        onClose={() => {
          if (!saving) setEditing(undefined)
        }}
        fullWidth
        maxWidth="xs"
      >
        <Box
          component="form"
          onSubmit={(event) => {
            void save(event)
          }}
        >
          <DialogTitle>
            {editing ? 'Editar categoria' : 'Nova categoria'}
          </DialogTitle>

          <DialogContent>
            <Stack gap={2} mt={1}>
              {error && <Alert severity="error">{error}</Alert>}

              <TextField
                autoFocus
                required
                fullWidth
                label="Nome"
                value={name}
                onChange={(event) => setName(event.target.value)}
                slotProps={{
                  htmlInput: {
                    maxLength: 100,
                  },
                }}
              />
            </Stack>
          </DialogContent>

          <DialogActions>
            <Button
              onClick={() => setEditing(undefined)}
              disabled={saving}
            >
              Cancelar
            </Button>

            <Button
              type="submit"
              variant="contained"
              disabled={saving || !name.trim()}
            >
              {saving ? (
                <CircularProgress size={20} color="inherit" />
              ) : (
                'Salvar'
              )}
            </Button>
          </DialogActions>
        </Box>
      </Dialog>

      <Dialog
        open={Boolean(deleting)}
        onClose={() => {
          if (!saving) setDeleting(null)
        }}
      >
        <DialogTitle>Excluir categoria</DialogTitle>

        <DialogContent>
          <Typography>
            Deseja excluir a categoria {deleting?.name}?
          </Typography>
        </DialogContent>

        <DialogActions>
          <Button
            onClick={() => setDeleting(null)}
            disabled={saving}
          >
            Voltar
          </Button>

          <Button
            color="error"
            variant="contained"
            onClick={() => {
              void remove()
            }}
            disabled={saving}
          >
            {saving ? (
              <CircularProgress size={20} color="inherit" />
            ) : (
              'Excluir'
            )}
          </Button>
        </DialogActions>
      </Dialog>
    </PageContainer>
  )
}
