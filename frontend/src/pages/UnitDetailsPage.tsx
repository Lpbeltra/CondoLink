import {
  useCallback,
  useEffect,
  useState,
  type FormEvent,
} from 'react'
import ArrowBackRoundedIcon from '@mui/icons-material/ArrowBackRounded'
import AddRoundedIcon from '@mui/icons-material/AddRounded'
import DeleteOutlineRoundedIcon from '@mui/icons-material/DeleteOutlineRounded'
import EditRoundedIcon from '@mui/icons-material/EditRounded'
import {
  Alert,
  Autocomplete,
  Box,
  Button,
  Card,
  CardContent,
  Checkbox,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControlLabel,
  IconButton,
  List,
  ListItem,
  ListItemText,
  MenuItem,
  Skeleton,
  Stack,
  TextField,
  Typography,
} from '@mui/material'
import { useNavigate, useParams } from 'react-router-dom'
import { EmptyState } from '../components/EmptyState'
import { PageContainer } from '../components/PageContainer'
import { useManagementContext } from '../management/ManagementContext'
import {
  createUnitMembership,
  deleteUnitMembership,
  getUnit,
  listBlocks,
  listCondominiumMembers,
  listUnitMemberships,
  updateUnit,
  updateUnitMembership,
} from '../management/api'
import { managementError } from '../management/errors'
import { sortBlocks } from '../management/unitPresentation'
import type {
  CondominiumBlock,
  CondominiumMember,
  RelationshipType,
  Unit,
  UnitMembership,
} from '../management/types'

const labels: Record<RelationshipType, string> = {
  Owner: 'Proprietário',
  Tenant: 'Inquilino',
  AuthorizedOccupant: 'Ocupante autorizado',
}

export function UnitDetailsPage() {
  const { unitId = '' } = useParams()
  const navigate = useNavigate()

  const {
    activeCondominiumId: condominiumId,
    isLoading: isManagementContextLoading,
  } = useManagementContext()

  const [unit, setUnit] = useState<Unit | null>(null)
  const [links, setLinks] = useState<UnitMembership[]>([])
  const [members, setMembers] = useState<CondominiumMember[]>([])
  const [blocks, setBlocks] = useState<CondominiumBlock[]>([])

  const [identifier, setIdentifier] = useState('')
  const [block, setBlock] = useState<CondominiumBlock | null>(null)
  const [description, setDescription] = useState('')

  const [dialogOpen, setDialogOpen] = useState(false)
  const [editing, setEditing] = useState<UnitMembership | null>(null)
  const [removing, setRemoving] = useState<UnitMembership | null>(null)

  const [userId, setUserId] = useState('')
  const [type, setType] = useState<RelationshipType>('Owner')
  const [resident, setResident] = useState(false)
  const [primary, setPrimary] = useState(false)

  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')

  const load = useCallback(async () => {
    if (isManagementContextLoading) {
      return
    }

    if (!condominiumId) {
      setLoading(false)
      setUnit(null)
      return
    }

    setLoading(true)
    setError('')

    try {
      const [loadedUnit, loadedLinks, loadedMembers, loadedBlocks] =
        await Promise.all([
          getUnit(unitId),
          listUnitMemberships(unitId),
          listCondominiumMembers(condominiumId),
          listBlocks(condominiumId),
        ])

      if (loadedUnit.condominiumId !== condominiumId) {
        throw new Error('wrong condominium')
      }

      const orderedBlocks = sortBlocks(loadedBlocks)

      setUnit(loadedUnit)
      setLinks(
        loadedLinks.filter((item) => item.membershipActive)
      )
      setMembers(loadedMembers)
      setBlocks(orderedBlocks)

      setIdentifier(loadedUnit.identifier)
      setBlock(
        orderedBlocks.find(
          (item) => item.id === loadedUnit.blockId
        ) ?? null
      )
      setDescription(loadedUnit.description ?? '')
    } catch (requestError) {
      setUnit(null)
      setError(managementError(requestError))
    } finally {
      setLoading(false)
    }
  }, [
    condominiumId,
    isManagementContextLoading,
    unitId,
  ])

  useEffect(() => {
    void load()
  }, [load])

  const saveUnit = async (event: FormEvent) => {
    event.preventDefault()

    if (
      !condominiumId ||
      saving ||
      !identifier.trim() ||
      (blocks.length > 0 && !block)
    ) {
      return
    }

    setSaving(true)
    setError('')
    setSuccess('')

    try {
      await updateUnit(condominiumId, unitId, {
        identifier: identifier.trim(),
        blockId: block?.id ?? null,
        description: description.trim() || null,
      })

      setSuccess('Unidade atualizada com sucesso.')
      await load()
    } catch (requestError) {
      setError(managementError(requestError))
    } finally {
      setSaving(false)
    }
  }

  const openCreate = () => {
    setEditing(null)
    setUserId('')
    setType('Owner')
    setResident(false)
    setPrimary(false)
    setDialogOpen(true)
  }

  const openEdit = (link: UnitMembership) => {
    setEditing(link)
    setUserId(link.userId)
    setType(link.relationshipType)
    setResident(link.isResident)
    setPrimary(link.isPrimaryResidence)
    setDialogOpen(true)
  }

  const saveLink = async (event: FormEvent) => {
    event.preventDefault()

    if (!userId || saving) {
      return
    }

    setSaving(true)
    setError('')
    setSuccess('')

    try {
      const payload = {
        relationshipType: type,
        isResident: resident,
        isPrimaryResidence: primary,
      }

      if (editing) {
        await updateUnitMembership(
          unitId,
          editing.unitMembershipId,
          payload
        )
      } else {
        await createUnitMembership(unitId, {
          userId,
          ...payload,
        })
      }

      setDialogOpen(false)

      setSuccess(
        editing
          ? 'Vínculo atualizado com sucesso.'
          : 'Vínculo adicionado com sucesso.'
      )

      await load()
    } catch (requestError) {
      setError(managementError(requestError))
    } finally {
      setSaving(false)
    }
  }

  const removeLink = async () => {
    if (!removing || saving) {
      return
    }

    const removedId = removing.unitMembershipId

    setSaving(true)
    setError('')
    setSuccess('')

    try {
      await deleteUnitMembership(unitId, removedId)

      setLinks((current) =>
        current.filter(
          (link) => link.unitMembershipId !== removedId
        )
      )

      setRemoving(null)
      setSuccess('Vínculo removido com sucesso.')
    } catch (requestError) {
      setRemoving(null)
      setError(managementError(requestError))
    } finally {
      setSaving(false)
    }
  }

  if (isManagementContextLoading || loading) {
    return (
      <PageContainer>
        <Skeleton variant="rounded" height={480} />
      </PageContainer>
    )
  }

  if (!condominiumId) {
    return (
      <PageContainer>
        <Button
          color="inherit"
          startIcon={<ArrowBackRoundedIcon />}
          onClick={() => navigate('/management/units')}
        >
          Voltar
        </Button>

        <Alert severity="info" sx={{ mt: 2 }}>
          Selecione um condomínio administrativo para acessar a
          unidade.
        </Alert>
      </PageContainer>
    )
  }

  if (!unit) {
    return (
      <PageContainer>
        <Button
          color="inherit"
          startIcon={<ArrowBackRoundedIcon />}
          onClick={() => navigate('/management/units')}
        >
          Voltar
        </Button>

        <Alert severity="error" sx={{ mt: 2 }}>
          {error || 'Unidade não encontrada.'}
        </Alert>
      </PageContainer>
    )
  }

  return (
    <PageContainer>
      <Button
        color="inherit"
        startIcon={<ArrowBackRoundedIcon />}
        onClick={() => navigate('/management/units')}
      >
        Voltar para unidades
      </Button>

      <Typography variant="h1" mt={2}>
        Gestão da unidade
      </Typography>

      {success && (
        <Alert severity="success" sx={{ mt: 2 }}>
          {success}
        </Alert>
      )}

      {error && (
        <Alert severity="error" sx={{ mt: 2 }}>
          {error}
        </Alert>
      )}

      <Card elevation={0} sx={{ mt: 3 }}>
        <CardContent
          component="form"
          onSubmit={(event) => void saveUnit(event)}
        >
          <Stack gap={2}>
            <TextField
              required
              label="Identificação da unidade"
              value={identifier}
              onChange={(event) =>
                setIdentifier(event.target.value)
              }
              slotProps={{
                htmlInput: {
                  maxLength: 50,
                },
              }}
            />

            {blocks.length > 0 && (
              <Autocomplete
                options={blocks}
                value={block}
                onChange={(_, value) => setBlock(value)}
                getOptionLabel={(option) => option.identifier}
                isOptionEqualToValue={(option, value) =>
                  option.id === value.id
                }
                autoHighlight
                selectOnFocus
                renderInput={(params) => (
                  <TextField
                    {...params}
                    required
                    label="Bloco"
                  />
                )}
              />
            )}

            <TextField
              multiline
              minRows={3}
              label="Observação"
              value={description}
              onChange={(event) =>
                setDescription(event.target.value)
              }
              slotProps={{
                htmlInput: {
                  maxLength: 500,
                },
              }}
            />

            <Box display="flex" justifyContent="flex-end">
              <Button
                type="submit"
                variant="contained"
                disabled={
                  saving ||
                  !identifier.trim() ||
                  (blocks.length > 0 && !block)
                }
              >
                {saving ? (
                  <CircularProgress
                    size={20}
                    color="inherit"
                  />
                ) : (
                  'Salvar alterações'
                )}
              </Button>
            </Box>
          </Stack>
        </CardContent>
      </Card>

      <Stack
        direction={{
          xs: 'column',
          sm: 'row',
        }}
        justifyContent="space-between"
        gap={2}
        mt={3}
      >
        <Box>
          <Typography variant="h2">
            Pessoas vinculadas
          </Typography>

          <Typography color="text.secondary">
            Gerencie moradores, proprietários e ocupantes desta
            unidade.
          </Typography>
        </Box>

        <Button
          variant="contained"
          startIcon={<AddRoundedIcon />}
          onClick={openCreate}
        >
          Adicionar vínculo
        </Button>
      </Stack>

      {links.length === 0 ? (
        <EmptyState
          title="Nenhuma pessoa vinculada a esta unidade."
          description="Adicione um membro do condomínio para criar o primeiro vínculo."
        />
      ) : (
        <List
          sx={{
            mt: 2,
            bgcolor: 'background.paper',
            borderRadius: 2,
          }}
        >
          {links.map((link) => (
            <ListItem
              key={link.unitMembershipId}
              divider
              sx={{
                alignItems: 'flex-start',
                py: 1.5,
                pr: 12,
              }}
              secondaryAction={
                <Stack direction="row">
                  <IconButton
                    aria-label={`Editar vínculo de ${link.fullName}`}
                    onClick={() => openEdit(link)}
                  >
                    <EditRoundedIcon />
                  </IconButton>

                  <IconButton
                    color="error"
                    aria-label={`Remover vínculo de ${link.fullName}`}
                    onClick={() => setRemoving(link)}
                  >
                    <DeleteOutlineRoundedIcon />
                  </IconButton>
                </Stack>
              }
            >
              <ListItemText
                primary={link.fullName}
                secondary={`${labels[link.relationshipType]} · ${
                  link.isResident
                    ? 'Reside na unidade'
                    : 'Não reside'
                }${
                  link.isPrimaryResidence
                    ? ' · Residência principal'
                    : ''
                }`}
                primaryTypographyProps={{
                  fontWeight: 750,
                }}
                secondaryTypographyProps={{
                  sx: {
                    overflowWrap: 'anywhere',
                  },
                }}
              />
            </ListItem>
          ))}
        </List>
      )}

      <Dialog
        open={dialogOpen}
        onClose={() => {
          if (!saving) {
            setDialogOpen(false)
          }
        }}
        fullWidth
        maxWidth="sm"
      >
        <Box
          component="form"
          onSubmit={(event) => void saveLink(event)}
        >
          <DialogTitle>
            {editing ? 'Editar vínculo' : 'Adicionar vínculo'}
          </DialogTitle>

          <DialogContent>
            <Stack gap={2} pt={1}>
              <Autocomplete
                options={members.filter(
                  (member) => member.membershipActive
                )}
                value={
                  members.find(
                    (member) => member.userId === userId
                  ) ?? null
                }
                disabled={Boolean(editing)}
                onChange={(_, member) =>
                  setUserId(member?.userId ?? '')
                }
                getOptionLabel={(member) =>
                  member.cpf
                    ? `${member.cpf} • ${member.fullName}`
                    : member.fullName
                }
                isOptionEqualToValue={(option, value) =>
                  option.userId === value.userId
                }
                autoHighlight
                selectOnFocus
                filterOptions={(options, state) => {
                  const term = state.inputValue
                    .trim()
                    .toLocaleLowerCase('pt-BR')

                  const digits = term.replace(/\D/g, '')

                  return options.filter(
                    (member) =>
                      member.fullName
                        .toLocaleLowerCase('pt-BR')
                        .includes(term) ||
                      (digits.length > 0 &&
                        member.cpf
                          ?.replace(/\D/g, '')
                          .includes(digits))
                  )
                }}
                renderInput={(params) => (
                  <TextField
                    {...params}
                    required
                    label="Pessoa"
                    placeholder="Pesquisar por nome ou CPF"
                  />
                )}
              />

              <TextField
                select
                label="Tipo de vínculo"
                value={type}
                onChange={(event) =>
                  setType(
                    event.target.value as RelationshipType
                  )
                }
              >
                {Object.entries(labels).map(
                  ([value, label]) => (
                    <MenuItem key={value} value={value}>
                      {label}
                    </MenuItem>
                  )
                )}
              </TextField>

              <FormControlLabel
                control={
                  <Checkbox
                    checked={resident}
                    onChange={(event) => {
                      setResident(event.target.checked)

                      if (!event.target.checked) {
                        setPrimary(false)
                      }
                    }}
                  />
                }
                label="Reside na unidade"
              />

              <FormControlLabel
                control={
                  <Checkbox
                    checked={primary}
                    onChange={(event) => {
                      setPrimary(event.target.checked)

                      if (event.target.checked) {
                        setResident(true)
                      }
                    }}
                  />
                }
                label="Residência principal"
              />
            </Stack>
          </DialogContent>

          <DialogActions>
            <Button
              onClick={() => setDialogOpen(false)}
              disabled={saving}
            >
              Cancelar
            </Button>

            <Button
              type="submit"
              variant="contained"
              disabled={saving || !userId}
            >
              {saving ? (
                <CircularProgress size={20} color="inherit" />
              ) : (
                'Salvar vínculo'
              )}
            </Button>
          </DialogActions>
        </Box>
      </Dialog>

      <Dialog
        open={Boolean(removing)}
        onClose={() => {
          if (!saving) {
            setRemoving(null)
          }
        }}
      >
        <DialogTitle>Remover vínculo</DialogTitle>

        <DialogContent>
          <Typography>
            Deseja remover o vínculo de {removing?.fullName} com esta
            unidade?
          </Typography>
        </DialogContent>

        <DialogActions>
          <Button
            onClick={() => setRemoving(null)}
            disabled={saving}
          >
            Voltar
          </Button>

          <Button
            color="error"
            variant="contained"
            onClick={() => void removeLink()}
            disabled={saving}
          >
            {saving ? (
              <CircularProgress size={20} color="inherit" />
            ) : (
              'Remover vínculo'
            )}
          </Button>
        </DialogActions>
      </Dialog>
    </PageContainer>
  )
}