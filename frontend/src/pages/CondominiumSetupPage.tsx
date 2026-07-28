import { useMemo, useState, type ReactNode } from 'react'
import AddRoundedIcon from '@mui/icons-material/AddRounded'
import DeleteOutlineRoundedIcon from '@mui/icons-material/DeleteOutlineRounded'
import DownloadRoundedIcon from '@mui/icons-material/DownloadRounded'
import UploadFileRoundedIcon from '@mui/icons-material/UploadFileRounded'
import {
  Accordion,
  AccordionDetails,
  AccordionSummary,
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Checkbox,
  Chip,
  Divider,
  FormControlLabel,
  FormHelperText,
  IconButton,
  LinearProgress,
  MenuItem,
  Paper,
  Radio,
  RadioGroup,
  Stack,
  Step,
  StepLabel,
  Stepper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Typography,
  useMediaQuery,
  useTheme,
} from '@mui/material'
import ExpandMoreRoundedIcon from '@mui/icons-material/ExpandMoreRounded'
import { PageContainer } from '../components/PageContainer'
import { useManagementContext } from '../management/ManagementContext'
import {
  confirmSetup,
  downloadSetupTemplate,
  previewGeneratedSetup,
  previewSetup,
  previewSetupImport,
} from '../management/setupApi'
import {
  createGeneratorSegment,
  generateStructure,
} from '../management/setupGenerator'
import type {
  GeneratorTower,
  SetupConfirmation,
  SetupDraft,
  SetupPreview,
} from '../management/setupTypes'
import { managementError } from '../management/errors'
import { useParams } from 'react-router-dom'

type SetupMethod = 'import' | 'generator'

const emptyDraft = (noRegistrableUnits: boolean): SetupDraft => ({
  noRegistrableUnits,
  units: [],
  residents: [],
})

export function CondominiumSetupPage() {
  const { condominiumId: overwatchCondominiumId } = useParams()
  const { activeCondominiumId: managementCondominiumId } =
    useManagementContext()
  const activeCondominiumId =
    overwatchCondominiumId ?? managementCondominiumId
  const theme = useTheme()
  const compact = useMediaQuery(theme.breakpoints.down('sm'))
  const [step, setStep] = useState(0)
  const [method, setMethod] = useState<SetupMethod>('import')
  const [noUnits, setNoUnits] = useState(false)
  const [structureFile, setStructureFile] = useState<File | null>(null)
  const [residentsFile, setResidentsFile] = useState<File | null>(null)
  const [towers, setTowers] = useState<GeneratorTower[]>([
    {
      id: crypto.randomUUID(),
      name: 'Tower A',
      segments: [createGeneratorSegment()],
    },
  ])
  const [preview, setPreview] = useState<SetupPreview | null>(null)
  const [confirmation, setConfirmation] =
    useState<SetupConfirmation | null>(null)
  const [working, setWorking] = useState(false)
  const [error, setError] = useState('')
  const generated = useMemo(() => generateStructure(towers), [towers])

  if (!activeCondominiumId) {
    return (
      <PageContainer>
        <Alert severity="info">
          Selecione um condomínio para iniciar a configuração.
        </Alert>
      </PageContainer>
    )
  }

  const loadPreview = async () => {
    if (working) return
    setWorking(true)
    setError('')
    try {
      let result: SetupPreview
      if (noUnits) {
        result = await previewSetup(
          activeCondominiumId,
          emptyDraft(true),
        )
      } else if (method === 'import') {
        result = await previewSetupImport(
          activeCondominiumId,
          structureFile,
          residentsFile,
          false,
        )
      } else {
        if (generated.errors.length > 0) {
          setError(generated.errors[0].reason)
          return
        }
        if (residentsFile) {
          const residentPreview = await previewSetupImport(
            activeCondominiumId,
            null,
            residentsFile,
            false,
          )
          result = await previewGeneratedSetup(
            activeCondominiumId,
            towers,
            residentPreview.draft.residents,
          )
        } else {
          result = await previewGeneratedSetup(
            activeCondominiumId,
            towers,
            [],
          )
        }
      }
      setPreview(result)
      setStep(2)
    } catch (requestError) {
      setError(managementError(requestError))
    } finally {
      setWorking(false)
    }
  }

  const refreshPreview = async (draft: SetupDraft) => {
    setWorking(true)
    setError('')
    try {
      setPreview(await previewSetup(activeCondominiumId, draft))
    } catch (requestError) {
      setError(managementError(requestError))
    } finally {
      setWorking(false)
    }
  }

  const confirm = async () => {
    if (!preview || preview.errors.length > 0 || working) return
    setWorking(true)
    setError('')
    try {
      setConfirmation(await confirmSetup(
        activeCondominiumId,
        preview.draft,
      ))
      setStep(3)
    } catch (requestError) {
      setError(managementError(requestError))
    } finally {
      setWorking(false)
    }
  }

  const reset = () => {
    setStep(0)
    setPreview(null)
    setConfirmation(null)
    setError('')
    setStructureFile(null)
    setResidentsFile(null)
  }

  const canGeneratePreview = noUnits
    || (method === 'import'
      ? Boolean(structureFile || residentsFile)
      : generated.units.length > 0 && generated.errors.length === 0)

  return (
    <PageContainer maxWidth={1200}>
      <Stack gap={0.5}>
        <Typography variant="h1">Configuração do condomínio</Typography>
        <Typography color="text.secondary">
          Importe ou gere toda a estrutura com validação antes de salvar.
        </Typography>
      </Stack>

      <Stepper
        activeStep={step}
        orientation={compact ? 'vertical' : 'horizontal'}
        sx={{ mt: 3, mb: 3 }}
      >
        {['Escolher método', 'Preparar dados', 'Revisar', 'Concluir'].map(
          label => (
            <Step key={label}><StepLabel>{label}</StepLabel></Step>
          ),
        )}
      </Stepper>

      {working && <LinearProgress aria-label="Processando configuração" />}
      {error && <Alert severity="error" sx={{ mt: 2 }}>{error}</Alert>}

      {step === 0 && (
        <Card sx={{ mt: 2 }}>
          <CardContent>
            <Typography variant="h2">Como deseja começar?</Typography>
            <Typography color="text.secondary" mt={1}>
              Os métodos são independentes. O gerador também aceita uma
              planilha de moradores.
            </Typography>
            <RadioGroup
              value={method}
              onChange={event =>
                setMethod(event.target.value as SetupMethod)}
              sx={{ mt: 2 }}
            >
              <Paper variant="outlined" sx={{ p: 2, mb: 1 }}>
                <FormControlLabel
                  value="import"
                  control={<Radio />}
                  label="Importar planilhas CSV ou XLSX"
                />
                <Typography color="text.secondary" ml={4}>
                  Ideal para estruturas irregulares e cadastros já existentes.
                </Typography>
              </Paper>
              <Paper variant="outlined" sx={{ p: 2 }}>
                <FormControlLabel
                  value="generator"
                  control={<Radio />}
                  label="Gerar estrutura por torres e segmentos"
                />
                <Typography color="text.secondary" ml={4}>
                  Ideal para pavimentos com padrões repetidos.
                </Typography>
              </Paper>
            </RadioGroup>
            <Divider sx={{ my: 2 }} />
            <FormControlLabel
              control={(
                <Checkbox
                  checked={noUnits}
                  onChange={event => setNoUnits(event.target.checked)}
                />
              )}
              label="Este condomínio não tem unidades cadastráveis."
            />
            <FormHelperText>
              Use para clubes, prédios administrativos, áreas compartilhadas
              ou associações comerciais sem salas ou unidades registráveis.
            </FormHelperText>
            <Stack direction="row" justifyContent="flex-end" mt={3}>
              <Button variant="contained" onClick={() => setStep(1)}>
                Continuar
              </Button>
            </Stack>
          </CardContent>
        </Card>
      )}

      {step === 1 && (
        <Stack gap={2}>
          {noUnits ? (
            <Alert severity="info">
              Nenhuma unidade será criada. O restante do CondoLink continuará
              funcionando normalmente.
            </Alert>
          ) : method === 'import' ? (
            <SpreadsheetImport
              condominiumId={activeCondominiumId}
              structureFile={structureFile}
              residentsFile={residentsFile}
              onStructureFile={setStructureFile}
              onResidentsFile={setResidentsFile}
            />
          ) : (
            <>
              <GeneratorEditor towers={towers} onChange={setTowers} />
              <Paper variant="outlined" sx={{ p: 2 }}>
                <Typography fontWeight={800}>
                  Moradores (opcional)
                </Typography>
                <Typography color="text.secondary" mb={1}>
                  Depois de gerar as unidades, você pode vinculá-las usando o
                  modelo de moradores.
                </Typography>
                <FileButton
                  label="Selecionar planilha de moradores"
                  file={residentsFile}
                  onChange={setResidentsFile}
                />
              </Paper>
              {generated.errors.map(issue => (
                <Alert severity="error" key={`${issue.line}-${issue.reason}`}>
                  {issue.reason}
                </Alert>
              ))}
              <Alert severity="info">
                O gerador produzirá {generated.units.length} unidades.
              </Alert>
            </>
          )}
          <HelpPanels />
          <Stack direction="row" justifyContent="space-between">
            <Button onClick={() => setStep(0)}>Voltar</Button>
            <Button
              variant="contained"
              disabled={!canGeneratePreview || working}
              onClick={() => void loadPreview()}
            >
              Gerar e validar prévia
            </Button>
          </Stack>
        </Stack>
      )}

      {step === 2 && preview && (
        <PreviewStep
          preview={preview}
          working={working}
          onBack={() => setStep(1)}
          onConfirm={() => void confirm()}
          onRemoveUnit={(line) => void refreshPreview({
            ...preview.draft,
            units: preview.draft.units.filter(item => item.line !== line),
          })}
          onRemoveResident={(line) => void refreshPreview({
            ...preview.draft,
            residents: preview.draft.residents.filter(
              item => item.line !== line,
            ),
          })}
        />
      )}

      {step === 3 && confirmation && (
        <ConfirmationStep result={confirmation} onReset={reset} />
      )}
    </PageContainer>
  )
}

function SpreadsheetImport({
  condominiumId,
  structureFile,
  residentsFile,
  onStructureFile,
  onResidentsFile,
}: {
  condominiumId: string
  structureFile: File | null
  residentsFile: File | null
  onStructureFile: (file: File | null) => void
  onResidentsFile: (file: File | null) => void
}) {
  return (
    <Stack gap={2}>
      <Alert severity="info">
        Nada será salvo agora. Primeiro você verá uma prévia completa. Se
        houver qualquer erro, nenhuma linha será importada.
      </Alert>
      <Card>
        <CardContent>
          <Typography variant="h2">1. Baixe os modelos</Typography>
          <Typography color="text.secondary" mt={1}>
            Não renomeie as colunas, não exclua colunas obrigatórias e mantenha
            identificadores como 01, 001 e Store 01 no formato texto.
          </Typography>
          <Stack direction={{ xs: 'column', sm: 'row' }} gap={1} mt={2}>
            <Button
              startIcon={<DownloadRoundedIcon />}
              onClick={() => void downloadSetupTemplate(
                condominiumId,
                'structure',
              )}
            >
              Baixar modelo de estrutura
            </Button>
            <Button
              startIcon={<DownloadRoundedIcon />}
              onClick={() => void downloadSetupTemplate(
                condominiumId,
                'residents',
              )}
            >
              Baixar modelo de moradores
            </Button>
          </Stack>
        </CardContent>
      </Card>
      <Card>
        <CardContent>
          <Typography variant="h2">2. Preencha e selecione</Typography>
          <Typography color="text.secondary" mt={1} mb={2}>
            Envie um ou os dois arquivos. Campos opcionais podem ficar vazios.
          </Typography>
          <Stack direction={{ xs: 'column', sm: 'row' }} gap={2}>
            <FileButton
              label="Planilha de estrutura"
              file={structureFile}
              onChange={onStructureFile}
            />
            <FileButton
              label="Planilha de moradores"
              file={residentsFile}
              onChange={onResidentsFile}
            />
          </Stack>
        </CardContent>
      </Card>
      <ColumnGuide />
    </Stack>
  )
}

function FileButton({
  label,
  file,
  onChange,
}: {
  label: string
  file: File | null
  onChange: (file: File | null) => void
}) {
  return (
    <Stack alignItems="flex-start" gap={0.5}>
      <Button
        component="label"
        variant="outlined"
        startIcon={<UploadFileRoundedIcon />}
      >
        {label}
        <input
          hidden
          type="file"
          accept=".csv,.xlsx"
          onChange={event => onChange(event.target.files?.[0] ?? null)}
        />
      </Button>
      <Typography variant="caption" color="text.secondary">
        {file?.name ?? 'CSV ou XLSX, até 5 MB.'}
      </Typography>
    </Stack>
  )
}

function ColumnGuide() {
  const rows = [
    ['Block', 'Não', 'Deixe vazio quando não houver blocos.', 'Tower A'],
    ['Unit', 'Sim', 'Identificador textual da unidade.', '101 / 01 / House 4'],
    ['Floor', 'Não', 'Andar exibido ao usuário.', 'Ground / 1 / Roof'],
    ['Description', 'Não', 'Descrição adicional.', 'Commercial Store'],
  ]
  return (
    <Accordion defaultExpanded>
      <AccordionSummary expandIcon={<ExpandMoreRoundedIcon />}>
        <Typography fontWeight={800}>Como preencher a estrutura</Typography>
      </AccordionSummary>
      <AccordionDetails>
        <TableContainer>
          <Table size="small">
            <TableHead>
              <TableRow>
                {['Coluna', 'Obrigatória', 'Descrição', 'Exemplo'].map(
                  value => <TableCell key={value}>{value}</TableCell>,
                )}
              </TableRow>
            </TableHead>
            <TableBody>
              {rows.map(row => (
                <TableRow key={row[0]}>
                  {row.map(value => (
                    <TableCell key={value}>{value}</TableCell>
                  ))}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
        <Typography mt={2}>
          Na planilha de moradores, Name e Email são obrigatórios. Relationship
          aceita Owner, Tenant ou AuthorizedOccupant. Resident e
          PrimaryResidence aceitam Yes/No ou Sim/Não.
        </Typography>
      </AccordionDetails>
    </Accordion>
  )
}

function GeneratorEditor({
  towers,
  onChange,
}: {
  towers: GeneratorTower[]
  onChange: (towers: GeneratorTower[]) => void
}) {
  const updateTower = (
    towerId: string,
    updater: (tower: GeneratorTower) => GeneratorTower,
  ) => onChange(towers.map(
    tower => tower.id === towerId ? updater(tower) : tower,
  ))

  return (
    <Stack gap={2}>
      <Alert severity="info">
        Cada segmento representa uma faixa de andares com a mesma quantidade e
        padrão de unidades. Crie novos segmentos quando o padrão mudar.
      </Alert>
      {towers.map((tower, towerIndex) => (
        <Card key={tower.id}>
          <CardContent>
            <Stack direction="row" alignItems="flex-start" gap={1}>
              <TextField
                fullWidth
                label={`Torre ou bloco ${towerIndex + 1}`}
                value={tower.name}
                onChange={event => updateTower(tower.id, current => ({
                  ...current,
                  name: event.target.value,
                }))}
                helperText="Opcional quando o condomínio não utiliza blocos."
              />
              {towers.length > 1 && (
                <IconButton
                  aria-label={`Remover torre ${towerIndex + 1}`}
                  onClick={() => onChange(
                    towers.filter(item => item.id !== tower.id),
                  )}
                >
                  <DeleteOutlineRoundedIcon />
                </IconButton>
              )}
            </Stack>
            {tower.segments.map((segment, segmentIndex) => (
              <Paper
                key={segment.id}
                variant="outlined"
                sx={{ p: 2, mt: 2 }}
              >
                <Stack
                  direction="row"
                  justifyContent="space-between"
                  alignItems="center"
                >
                  <Typography fontWeight={800}>
                    Segmento {segmentIndex + 1}
                  </Typography>
                  {tower.segments.length > 1 && (
                    <IconButton
                      aria-label={`Remover segmento ${segmentIndex + 1}`}
                      onClick={() => updateTower(tower.id, current => ({
                        ...current,
                        segments: current.segments.filter(
                          item => item.id !== segment.id,
                        ),
                      }))}
                    >
                      <DeleteOutlineRoundedIcon />
                    </IconButton>
                  )}
                </Stack>
                <Box
                  display="grid"
                  gridTemplateColumns={{
                    xs: '1fr',
                    sm: 'repeat(2, 1fr)',
                    md: 'repeat(3, 1fr)',
                  }}
                  gap={2}
                  mt={1}
                >
                  <NumberField
                    label="Andar inicial"
                    value={segment.startFloor}
                    helper="Use 0 para térreo ou informe o primeiro andar."
                    onChange={value => updateTowerSegment(
                      tower,
                      segment.id,
                      { startFloor: value },
                      updateTower,
                    )}
                  />
                  <NumberField
                    label="Andar final"
                    value={segment.endFloor}
                    helper="Último andar que repete este padrão."
                    onChange={value => updateTowerSegment(
                      tower,
                      segment.id,
                      { endFloor: value },
                      updateTower,
                    )}
                  />
                  <NumberField
                    label="Unidades por andar"
                    value={segment.unitsPerFloor}
                    helper="Número de unidades geradas em cada andar."
                    onChange={value => updateTowerSegment(
                      tower,
                      segment.id,
                      { unitsPerFloor: value },
                      updateTower,
                    )}
                  />
                  <NumberField
                    label="Primeiro número"
                    value={segment.firstUnit}
                    helper="Número inicial dentro do andar ou da sequência."
                    onChange={value => updateTowerSegment(
                      tower,
                      segment.id,
                      { firstUnit: value },
                      updateTower,
                    )}
                  />
                  <NumberField
                    label="Dígitos"
                    value={segment.digits}
                    helper="Quantidade mínima de dígitos: 1 vira 01 com 2."
                    onChange={value => updateTowerSegment(
                      tower,
                      segment.id,
                      { digits: Math.max(1, value) },
                      updateTower,
                    )}
                  />
                  <TextField
                    select
                    label="Incluir número do andar"
                    value={segment.includeFloorNumber ? 'yes' : 'no'}
                    onChange={event => updateTowerSegment(
                      tower,
                      segment.id,
                      {
                        includeFloorNumber: event.target.value === 'yes',
                      },
                      updateTower,
                    )}
                    helperText="Sim gera 101; Não permite sequências como 01."
                  >
                    <MenuItem value="yes">Sim</MenuItem>
                    <MenuItem value="no">Não</MenuItem>
                  </TextField>
                  <TextField
                    label="Prefixo"
                    value={segment.prefix}
                    onChange={event => updateTowerSegment(
                      tower,
                      segment.id,
                      { prefix: event.target.value },
                      updateTower,
                    )}
                    helperText="Valor opcional antes do número, como A-."
                  />
                  <TextField
                    label="Sufixo"
                    value={segment.suffix}
                    onChange={event => updateTowerSegment(
                      tower,
                      segment.id,
                      { suffix: event.target.value },
                      updateTower,
                    )}
                    helperText="Valor opcional depois do número, como -A."
                  />
                </Box>
              </Paper>
            ))}
            <Button
              startIcon={<AddRoundedIcon />}
              sx={{ mt: 2 }}
              onClick={() => updateTower(tower.id, current => ({
                ...current,
                segments: [
                  ...current.segments,
                  createGeneratorSegment(),
                ],
              }))}
            >
              Adicionar segmento
            </Button>
          </CardContent>
        </Card>
      ))}
      <Button
        startIcon={<AddRoundedIcon />}
        onClick={() => onChange([
          ...towers,
          {
            id: crypto.randomUUID(),
            name: `Tower ${String.fromCharCode(65 + towers.length)}`,
            segments: [createGeneratorSegment()],
          },
        ])}
      >
        Adicionar torre
      </Button>
    </Stack>
  )
}

function updateTowerSegment(
  tower: GeneratorTower,
  segmentId: string,
  changes: Partial<GeneratorTower['segments'][number]>,
  updateTower: (
    towerId: string,
    updater: (tower: GeneratorTower) => GeneratorTower,
  ) => void,
) {
  updateTower(tower.id, current => ({
    ...current,
    segments: current.segments.map(
      segment => segment.id === segmentId
        ? { ...segment, ...changes }
        : segment,
    ),
  }))
}

function NumberField({
  label,
  value,
  helper,
  onChange,
}: {
  label: string
  value: number
  helper: string
  onChange: (value: number) => void
}) {
  return (
    <TextField
      type="number"
      label={label}
      value={value}
      onChange={event => onChange(Number(event.target.value))}
      helperText={helper}
    />
  )
}

function HelpPanels() {
  return (
    <Stack>
      <Accordion>
        <AccordionSummary expandIcon={<ExpandMoreRoundedIcon />}>
          <Typography fontWeight={800}>Numerações suportadas</Typography>
        </AccordionSummary>
        <AccordionDetails>
          <Typography>
            01, 001, 101, 1001, A-101, 101-A, House 2, Store 01,
            coberturas e outros identificadores textuais são aceitos.
          </Typography>
        </AccordionDetails>
      </Accordion>
      <Accordion>
        <AccordionSummary expandIcon={<ExpandMoreRoundedIcon />}>
          <Typography fontWeight={800}>Erros comuns e boas práticas</Typography>
        </AccordionSummary>
        <AccordionDetails>
          <Typography component="div">
            Não renomeie colunas; formate identificadores como texto; não
            repita a mesma unidade no mesmo bloco; confira e-mails; use um novo
            segmento quando a quantidade de unidades mudar.
          </Typography>
        </AccordionDetails>
      </Accordion>
      <Accordion>
        <AccordionSummary expandIcon={<ExpandMoreRoundedIcon />}>
          <Typography fontWeight={800}>Perguntas frequentes</Typography>
        </AccordionSummary>
        <AccordionDetails>
          <Typography>
            Blocos, andar, descrição, telefone e planilha de moradores são
            opcionais. Usuários encontrados pelo e-mail serão reutilizados.
            Nada é salvo antes da confirmação final.
          </Typography>
        </AccordionDetails>
      </Accordion>
    </Stack>
  )
}

function PreviewStep({
  preview,
  working,
  onBack,
  onConfirm,
  onRemoveUnit,
  onRemoveResident,
}: {
  preview: SetupPreview
  working: boolean
  onBack: () => void
  onConfirm: () => void
  onRemoveUnit: (line: number) => void
  onRemoveResident: (line: number) => void
}) {
  return (
    <Stack gap={2}>
      <Alert severity={preview.errors.length > 0 ? 'error' : 'success'}>
        {preview.errors.length > 0
          ? 'Corrija ou remova as linhas com erro. Nada foi salvo.'
          : 'Lote validado. Revise os dados antes de confirmar.'}
      </Alert>
      <Stack direction="row" flexWrap="wrap" gap={1}>
        <Chip label={`${preview.totals.blocks} blocos`} />
        <Chip label={`${preview.totals.units} unidades`} />
        <Chip label={`${preview.totals.residents} moradores`} />
        <Chip label={`${preview.totals.existingUsers} usuários reutilizados`} />
        <Chip color="primary" label={`${preview.totals.newUsers} novos usuários`} />
      </Stack>
      {preview.errors.length > 0 && (
        <IssueTable title="Erros" issues={preview.errors} severity="error" />
      )}
      {preview.warnings.length > 0 && (
        <IssueTable
          title="Avisos"
          issues={preview.warnings}
          severity="warning"
        />
      )}
      <PreviewTable
        title="Unidades"
        headers={['Linha', 'Bloco', 'Unidade', 'Andar', 'Situação', '']}
        rows={preview.units.map(row => [
          row.line,
          row.block ?? 'Sem bloco',
          row.unit,
          row.floor ?? '—',
          row.existing ? 'Será reutilizada' : 'Nova',
          <IconButton
            key="remove"
            aria-label={`Remover unidade ${row.unit}`}
            onClick={() => onRemoveUnit(row.line)}
          >
            <DeleteOutlineRoundedIcon />
          </IconButton>,
        ])}
      />
      <PreviewTable
        title="Moradores"
        headers={['Linha', 'Nome', 'E-mail', 'Unidade', 'Situação', '']}
        rows={preview.residents.map(row => [
          row.line,
          row.name,
          row.email,
          row.unit
            ? `${row.block ? `${row.block} / ` : ''}${row.unit}`
            : 'Sem unidade',
          row.existingUser ? 'Usuário reutilizado' : 'Novo usuário',
          <IconButton
            key="remove"
            aria-label={`Remover morador ${row.name}`}
            onClick={() => onRemoveResident(row.line)}
          >
            <DeleteOutlineRoundedIcon />
          </IconButton>,
        ])}
      />
      <Stack direction="row" justifyContent="space-between">
        <Button onClick={onBack}>Voltar e corrigir</Button>
        <Button
          variant="contained"
          disabled={working || preview.errors.length > 0}
          onClick={onConfirm}
        >
          Confirmar configuração
        </Button>
      </Stack>
    </Stack>
  )
}

function IssueTable({
  title,
  issues,
  severity,
}: {
  title: string
  issues: SetupPreview['errors']
  severity: 'error' | 'warning'
}) {
  return (
    <Alert severity={severity}>
      <Typography fontWeight={800}>{title}</Typography>
      {issues.map((issue, index) => (
        <Typography key={`${issue.line}-${issue.column}-${index}`}>
          {issue.line > 0 ? `Linha ${issue.line}, ` : ''}
          {issue.column}: {issue.reason}
        </Typography>
      ))}
    </Alert>
  )
}

function PreviewTable({
  title,
  headers,
  rows,
}: {
  title: string
  headers: string[]
  rows: (string | number | ReactNode)[][]
}) {
  return (
    <Paper variant="outlined">
      <Typography variant="h2" p={2}>{title}</Typography>
      {rows.length === 0 ? (
        <Typography color="text.secondary" px={2} pb={2}>
          Nenhum item.
        </Typography>
      ) : (
        <TableContainer>
          <Table size="small">
            <TableHead>
              <TableRow>
                {headers.map(header => (
                  <TableCell key={header}>{header}</TableCell>
                ))}
              </TableRow>
            </TableHead>
            <TableBody>
              {rows.map((row, rowIndex) => (
                <TableRow key={rowIndex}>
                  {row.map((value, columnIndex) => (
                    <TableCell key={columnIndex}>{value}</TableCell>
                  ))}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}
    </Paper>
  )
}

function ConfirmationStep({
  result,
  onReset,
}: {
  result: SetupConfirmation
  onReset: () => void
}) {
  return (
    <Stack gap={2}>
      <Alert severity="success">{result.message}</Alert>
      <Typography>
        {result.blocksCreated} blocos e {result.unitsCreated} unidades criados;
        {' '}{result.residentsLinked} moradores processados.
      </Typography>
      {result.credentials.length > 0 && (
        <Alert severity="warning">
          Copie estas credenciais agora. As senhas temporárias são exibidas
          somente nesta tela.
        </Alert>
      )}
      <PreviewTable
        title="Novas credenciais"
        headers={['Nome', 'E-mail', 'Senha temporária']}
        rows={result.credentials.map(item => [
          item.fullName,
          item.email,
          item.temporaryPassword,
        ])}
      />
      <Button variant="contained" onClick={onReset}>
        Finalizar
      </Button>
    </Stack>
  )
}
