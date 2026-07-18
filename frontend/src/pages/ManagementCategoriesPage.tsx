import { useCallback, useEffect, useState, type FormEvent } from 'react'
import AddRoundedIcon from '@mui/icons-material/AddRounded'
import DeleteOutlineRoundedIcon from '@mui/icons-material/DeleteOutlineRounded'
import EditRoundedIcon from '@mui/icons-material/EditRounded'
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
  List,
  ListItem,
  ListItemButton,
  ListItemText,
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
  const [name, setName] = useState('')

  const load = useCallback(async () => {
    if (!condominiumId) {
      setItems([])
      setLoading(false)
      return
    }

    setLoading(true)
    setError('')

    try {
      setItems(await listCategories(condominiumId))
    } catch (requestError) {
      setError(managementError(requestError))
    } finally {
      setLoading(false)
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

    try {
      if (editing) {
        await updateCategory(condominiumId, editing.id, name.trim())
      } else {
        await createCategory(condominiumId, {
          name: name.trim(),
          description: null,
        })
      }

      setEditing(undefined)
      setName('')
      setSuccess(
        editing
          ? 'Categoria atualizada com sucesso.'
          : 'Categoria criada com sucesso.',
      )

      await load()
    } catch (requestError) {
      setError(managementError(requestError))
    } finally {
      setSaving(false)
    }
  }

  const remove = async () => {
    if (!condominiumId || !deleting || saving) return

    setSaving(true)
    setError('')

    try {
      await deleteCategory(condominiumId, deleting.id)

      setDeleting(null)
      setSuccess('Categoria excluída com sucesso.')

      await load()
    } catch (requestError) {
      setDeleting(null)
      setError(managementError(requestError))
    } finally {
      setSaving(false)
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
            Organize os tipos de solicitação disponíveis aos moradores.
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
          Nova categoria
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
              description="Crie categorias para organizar as solicitações."
            />
          ) : visible.length === 0 ? (
            <EmptyState
              title="Nenhuma categoria encontrada."
              description="Revise o texto pesquisado."
            />
          ) : (
            <List
              sx={{
                mt: 2,
                bgcolor: 'background.paper',
                borderRadius: 2,
              }}
            >
              {visible.map((item) => (
                <ListItem
                  key={item.id}
                  disablePadding
                  divider
                  secondaryAction={
                    <Stack direction="row">
                      <IconButton
                        aria-label={`Editar ${item.name}`}
                        onClick={(event) => {
                          event.stopPropagation()
                          setEditing(item)
                          setName(item.name)
                          setError('')
                        }}
                      >
                        <EditRoundedIcon />
                      </IconButton>

                      <IconButton
                        color="error"
                        aria-label={`Excluir ${item.name}`}
                        onClick={(event) => {
                          event.stopPropagation()
                          setDeleting(item)
                        }}
                      >
                        <DeleteOutlineRoundedIcon />
                      </IconButton>
                    </Stack>
                  }
                >
                  <ListItemButton
                    onClick={() =>
                      navigate(
                        `/management/requests?categoryId=${item.id}`,
                      )
                    }
                    sx={{
                      py: 1.5,
                      pr: 12,
                    }}
                    aria-label={`Abrir atendimento da categoria ${item.name}`}
                  >
                    <ListItemText
                      primary={item.name}
                      secondary={`${item.requestCount} ${
                        item.requestCount === 1
                          ? 'solicitação'
                          : 'solicitações'
                      }`}
                      primaryTypographyProps={{
                        fontWeight: 750,
                      }}
                    />
                  </ListItemButton>
                </ListItem>
              ))}
            </List>
          )}
        </>
      )}

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