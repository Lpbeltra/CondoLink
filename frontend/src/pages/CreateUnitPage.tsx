import { useEffect, useState, type FormEvent } from 'react'
import ArrowBackRoundedIcon from '@mui/icons-material/ArrowBackRounded'
import {
  Alert,
  Autocomplete,
  Button,
  Card,
  CardContent,
  Skeleton,
  Stack,
  TextField,
  Typography,
} from '@mui/material'
import { useNavigate } from 'react-router-dom'
import { PageContainer } from '../components/PageContainer'
import { useManagementContext } from '../management/ManagementContext'
import { createUnit, listBlocks } from '../management/api'
import { managementError } from '../management/errors'
import { sortBlocks } from '../management/unitPresentation'
import type { CondominiumBlock } from '../management/types'

export function CreateUnitPage() {
  const navigate = useNavigate()

  const {
    activeCondominiumId,
    isLoading: isManagementContextLoading,
  } = useManagementContext()

  const [identifier, setIdentifier] = useState('')
  const [block, setBlock] = useState<CondominiumBlock | null>(null)
  const [description, setDescription] = useState('')
  const [blocks, setBlocks] = useState<CondominiumBlock[]>([])
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')

  useEffect(() => {
    setBlock(null)
    setBlocks([])
    setError('')

    if (!activeCondominiumId) {
      setLoading(false)
      return
    }

    let cancelled = false

    setLoading(true)

    listBlocks(activeCondominiumId)
      .then((data) => {
        if (cancelled) return

        setBlocks(sortBlocks(data))
      })
      .catch((requestError) => {
        if (cancelled) return

        setError(managementError(requestError))
      })
      .finally(() => {
        if (!cancelled) {
          setLoading(false)
        }
      })

    return () => {
      cancelled = true
    }
  }, [activeCondominiumId])

  const submit = async (event: FormEvent) => {
    event.preventDefault()

    if (
      !identifier.trim() ||
      saving ||
      !activeCondominiumId ||
      (blocks.length > 0 && !block)
    ) {
      return
    }

    setSaving(true)
    setError('')

    try {
      const unit = await createUnit(activeCondominiumId, {
        identifier: identifier.trim(),
        blockId: block?.id ?? null,
        floor: null,
        description: description.trim() || null,
      })

      navigate(`/management/units/${unit.id}`, {
        state: {
          created: true,
        },
      })
    } catch (requestError) {
      setError(managementError(requestError))
    } finally {
      setSaving(false)
    }
  }

  const isLoading = isManagementContextLoading || loading

  const submitDisabled =
    saving ||
    !activeCondominiumId ||
    !identifier.trim() ||
    (blocks.length > 0 && !block)

  return (
    <PageContainer>
      <Button
        color="inherit"
        startIcon={<ArrowBackRoundedIcon />}
        onClick={() => navigate('/management/units')}
      >
        Voltar
      </Button>

      <Typography variant="h1" mt={2}>
        Nova unidade
      </Typography>

      <Card
        elevation={0}
        sx={{
          mt: 3,
          maxWidth: 720,
        }}
      >
        <CardContent
          component="form"
          onSubmit={(event) => void submit(event)}
          sx={{
            p: {
              xs: 2.5,
              sm: 4,
            },
          }}
        >
          <Stack gap={2}>
            {error && (
              <Alert severity="error">
                {error}
              </Alert>
            )}

            {!isLoading && !activeCondominiumId && (
              <Alert severity="info">
                Selecione um condomínio administrativo antes de cadastrar
                uma unidade.
              </Alert>
            )}

            {isLoading ? (
              <Skeleton height={150} />
            ) : activeCondominiumId ? (
              <>
                <TextField
                  required
                  label="Identificação"
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
                  label="Observação"
                  multiline
                  minRows={3}
                  value={description}
                  onChange={(event) =>
                    setDescription(event.target.value)
                  }
                  slotProps={{
                    htmlInput: {
                      maxLength: 500,
                    },
                  }}
                  helperText={`${description.length}/500`}
                />

                <Button
                  type="submit"
                  variant="contained"
                  disabled={submitDisabled}
                >
                  {saving ? 'Salvando...' : 'Cadastrar unidade'}
                </Button>
              </>
            ) : null}
          </Stack>
        </CardContent>
      </Card>
    </PageContainer>
  )
}