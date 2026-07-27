import { useCallback, useEffect, useState } from 'react'
import AddRoundedIcon from '@mui/icons-material/AddRounded'
import EditRoundedIcon from '@mui/icons-material/EditRounded'
import OpenInNewRoundedIcon from '@mui/icons-material/OpenInNewRounded'
import {
  Alert,
  Box,
  Button,
  Chip,
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
  createManagementCompany,
  listManagementCompanies,
  updateManagementCompany,
} from '../managementCompanies/api'
import { managementCompanyError } from '../managementCompanies/errors'
import { ManagementCompanyFormDialog } from '../managementCompanies/ManagementCompanyFormDialog'
import {
  managementCompanyDetailsPath,
  upsertManagementCompany,
} from '../managementCompanies/presentation'
import type {
  ManagementCompany,
  ManagementCompanyInput,
} from '../managementCompanies/types'
import { formatCnpj } from '../registration'

export function OverwatchManagementCompaniesPage() {
  const navigate = useNavigate()
  const [companies, setCompanies] = useState<ManagementCompany[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState('')
  const [formCompany, setFormCompany] = useState<ManagementCompany | null | undefined>()
  const [formError, setFormError] = useState('')
  const [isSaving, setIsSaving] = useState(false)
  const [feedback, setFeedback] = useState('')

  const load = useCallback(async () => {
    setIsLoading(true)
    setError('')
    try {
      setCompanies(await listManagementCompanies())
    } catch (requestError) {
      setError(managementCompanyError(requestError))
    } finally {
      setIsLoading(false)
    }
  }, [])

  useEffect(() => {
    void load()
  }, [load])

  const save = async (input: ManagementCompanyInput) => {
    if (isSaving) return
    setIsSaving(true)
    setFormError('')
    try {
      const saved = formCompany
        ? await updateManagementCompany(formCompany.id, input)
        : await createManagementCompany(input)
      setCompanies((current) => upsertManagementCompany(current, saved))
      setFormCompany(undefined)
      setFeedback(formCompany ? 'Administradora atualizada.' : 'Administradora criada.')
    } catch (requestError) {
      setFormError(managementCompanyError(requestError))
    } finally {
      setIsSaving(false)
    }
  }

  const manage = (id: string) => {
    navigate(managementCompanyDetailsPath(id))
  }

  return (
    <PageContainer>
      <Stack direction={{ xs: 'column', sm: 'row' }} justifyContent="space-between" gap={2}>
        <Box>
          <Typography variant="h1">Administradoras</Typography>
          <Typography color="text.secondary" mt={1}>
            Consulte as empresas responsáveis pela gestão dos condomínios.
          </Typography>
        </Box>
        <Button
          variant="contained"
          startIcon={<AddRoundedIcon />}
          onClick={() => {
            setFormError('')
            setFormCompany(null)
          }}
        >
          Nova administradora
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
        <Skeleton variant="rounded" height={260} sx={{ mt: 3 }} />
      ) : companies.length === 0 ? (
        <Box mt={3}>
          <EmptyState
            title="Nenhuma administradora cadastrada"
            description="Cadastre a primeira administradora da plataforma."
            actionLabel="Nova administradora"
            onAction={() => setFormCompany(null)}
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
                {['Nome', 'Condomínios', 'Funcionários', 'Status', 'Ações'].map((column) => (
                  <TableCell key={column} sx={{ fontWeight: 750 }}>{column}</TableCell>
                ))}
              </TableRow>
            </TableHead>
            <TableBody>
              {companies.map((company) => (
                <TableRow
                  key={company.id}
                  hover
                  onClick={() => manage(company.id)}
                  sx={{ cursor: 'pointer' }}
                >
                  <TableCell>
                    <Typography fontWeight={750}>{company.name}</Typography>
                    <Typography color="text.secondary" fontSize=".82rem">
                      {formatCnpj(company.cnpj)}
                      {company.city && company.state ? ` · ${company.city}/${company.state}` : ''}
                    </Typography>
                  </TableCell>
                  <TableCell>{company.condominiumCount}</TableCell>
                  <TableCell>{company.employeeCount}</TableCell>
                  <TableCell>
                    <Chip
                      size="small"
                      color={company.isActive ? 'success' : 'default'}
                      label={company.isActive ? 'Ativa' : 'Inativa'}
                    />
                  </TableCell>
                  <TableCell onClick={(event) => event.stopPropagation()}>
                    <Tooltip title="Editar">
                      <IconButton
                        aria-label={`Editar ${company.name}`}
                        onClick={() => {
                          setFormError('')
                          setFormCompany(company)
                        }}
                      >
                        <EditRoundedIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                    <Button
                      size="small"
                      endIcon={<OpenInNewRoundedIcon />}
                      onClick={() => manage(company.id)}
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

      <ManagementCompanyFormDialog
        open={formCompany !== undefined}
        company={formCompany}
        isSaving={isSaving}
        error={formError}
        onClose={() => setFormCompany(undefined)}
        onSubmit={save}
      />
      <TransientFeedback
        message={feedback}
        severity="success"
        onClose={() => setFeedback('')}
      />
    </PageContainer>
  )
}
