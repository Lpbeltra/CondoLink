import { useEffect, useRef, useState } from 'react'
import MoreVertRoundedIcon from '@mui/icons-material/MoreVertRounded'
import {
  Alert, Box, Button, Chip, Dialog, DialogActions, DialogContent, DialogContentText, DialogTitle, IconButton, LinearProgress,
  Menu, MenuItem, Paper, Stack, Table, TableBody, TableCell, TableHead, TableRow, TextField, Typography,
} from '@mui/material'
import { listEmployees, type Employee } from '../employees/api'
import { getErrorMessage } from '../services/api'
import {
  confirmBatch, deleteDocument, distributeBatch, getBatch, getDistributionSummary, listBatches, listDeliveries,
  previewDocumentUrl, replaceDocumentFile, reopenBatch, resendDocument, updateDocumentAssociation, uploadBatch,
  type DistributionSummary, type EmployeeDocument, type EmployeeDocumentBatch, type EmployeeDocumentDelivery,
} from './api'

const monthNames = ['Janeiro', 'Fevereiro', 'Março', 'Abril', 'Maio', 'Junho', 'Julho', 'Agosto', 'Setembro', 'Outubro', 'Novembro', 'Dezembro']
const competenceLabel = (month: number, year: number) => `${monthNames[month - 1]}/${year}`

const confidenceLabel: Record<string, string> = { High: 'Alta confiança', Medium: 'Média confiança', Low: 'Baixa confiança', None: '' }
const statusIcon: Record<string, string> = { Identified: '✓', Confirmed: '✓', NeedsReview: '⚠', Unidentified: '?', Ignored: '—' }
// "Concluído" means the distribution round finished — not that every holerite
// was delivered successfully. Individual delivered/lido/falhou counts are
// always shown separately (see DistributionView) and are never folded into this label.
const batchStatusLabel: Record<string, string> = {
  Uploaded: 'Enviado', Processing: 'Processando', ReadyForReview: 'Pronto para revisão',
  Confirmed: 'Confirmado', Distributing: 'Distribuindo', Completed: 'Concluído', Failed: 'Falhou',
}

type View = 'history' | 'upload' | 'review' | 'distribution'

export function PayslipDistribution() {
  const [view, setView] = useState<View>('history')
  const [batches, setBatches] = useState<EmployeeDocumentBatch[]>([])
  const [loadingHistory, setLoadingHistory] = useState(true)
  const [activeBatchId, setActiveBatchId] = useState<string | null>(null)
  const [error, setError] = useState('')

  const loadHistory = () => {
    setLoadingHistory(true)
    return listBatches().then(setBatches).catch(() => setError('Não foi possível carregar o histórico.'))
      .finally(() => setLoadingHistory(false))
  }
  // The administrator-wide history is independent of a selected condominium.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  useEffect(() => { void loadHistory() }, [])

  const openBatch = (batch: EmployeeDocumentBatch) => {
    setActiveBatchId(batch.id)
    setView(batch.status === 'Confirmed' || batch.status === 'Distributing' || batch.status === 'Completed' ? 'distribution' : 'review')
  }

  return (
    <Stack gap={2}>
      <Box display="flex" justifyContent="space-between" gap={2} flexWrap="wrap">
        <Box>
          <Typography variant="h2" fontSize={20} fontWeight={800}>Holerites</Typography>
          <Typography color="text.secondary">Distribuição de holerites via WhatsApp, por competência.</Typography>
        </Box>
        {view !== 'upload' && <Button variant="contained" onClick={() => setView('upload')}>Distribuir holerites</Button>}
      </Box>
      {error && <Alert severity="error" onClose={() => setError('')}>{error}</Alert>}

      {view === 'history' && (
        <BatchHistory loading={loadingHistory} batches={batches} onOpen={openBatch} />
      )}
      {view === 'upload' && (
        <UploadBatch
          onCancel={() => setView('history')}
          onUploaded={batchId => { setActiveBatchId(batchId); setView('review') }} />
      )}
      {view === 'review' && activeBatchId && (
        <ReviewBatch batchId={activeBatchId}
          onBack={() => { setView('history'); void loadHistory() }}
          onConfirmed={() => setView('distribution')} />
      )}
      {view === 'distribution' && activeBatchId && (
        <DistributionView batchId={activeBatchId}
          onBack={() => { setView('history'); void loadHistory() }} />
      )}
    </Stack>
  )
}

function BatchHistory({ loading, batches, onOpen }: {
  loading: boolean; batches: EmployeeDocumentBatch[]; onOpen: (batch: EmployeeDocumentBatch) => void
}) {
  if (loading) return <Alert severity="info">Carregando…</Alert>
  if (batches.length === 0) return <Alert severity="info">Nenhum lote de holerites ainda.</Alert>
  return (
    <Paper variant="outlined" sx={{ overflowX: 'auto' }}>
      <Table>
        <TableHead><TableRow>
          {['Competência', 'Documentos', 'Status', 'Processado por', ''].map(x => <TableCell key={x}>{x}</TableCell>)}
        </TableRow></TableHead>
        <TableBody>
          {batches.map(batch => (
            <TableRow key={batch.id}>
              <TableCell>{competenceLabel(batch.competenceMonth, batch.competenceYear)}</TableCell>
              <TableCell>{batch.documentCount}</TableCell>
              <TableCell><Chip size="small" label={batchStatusLabel[batch.status] ?? batch.status} /></TableCell>
              <TableCell>{batch.createdByName}</TableCell>
              <TableCell align="right"><Button size="small" onClick={() => onOpen(batch)}>Abrir</Button></TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </Paper>
  )
}

function UploadBatch({ onCancel, onUploaded }: {
  onCancel: () => void; onUploaded: (batchId: string) => void
}) {
  const now = new Date()
  const [month, setMonth] = useState(now.getMonth() + 1)
  const [year, setYear] = useState(now.getFullYear())
  const [files, setFiles] = useState<File[]>([])
  const [uploading, setUploading] = useState(false)
  const [error, setError] = useState('')

  const submit = async () => {
    if (files.length === 0) { setError('Selecione ao menos um arquivo PDF.'); return }
    setUploading(true); setError('')
    try {
      const result = await uploadBatch(files, month, year)
      onUploaded(result.id)
    } catch (e) {
      setError(getErrorMessage(e))
    } finally {
      setUploading(false)
    }
  }

  return (
    <Paper variant="outlined" sx={{ p: 3 }}>
      <Stack gap={2} maxWidth={480}>
        <Typography variant="h3" fontSize={16} fontWeight={700}>Nova distribuição de holerites</Typography>
        {error && <Alert severity="error">{error}</Alert>}
        <Stack direction="row" gap={1.5}>
          <TextField select label="Mês" value={month} onChange={e => setMonth(Number(e.target.value))} sx={{ flex: 1 }}>
            {monthNames.map((name, index) => <MenuItem key={name} value={index + 1}>{name}</MenuItem>)}
          </TextField>
          <TextField label="Ano" type="number" value={year} onChange={e => setYear(Number(e.target.value))} sx={{ flex: 1 }} />
        </Stack>
        <Button component="label" variant="outlined">
          {files.length > 0 ? `${files.length} arquivo(s) selecionado(s)` : 'Selecionar PDFs'}
          <input type="file" accept="application/pdf" multiple hidden
            onChange={e => setFiles(Array.from(e.target.files ?? []))} />
        </Button>
        <Typography variant="body2" color="text.secondary">
          Comvy vai separar e identificar automaticamente cada holerite. Nada é enviado antes da sua revisão e confirmação.
        </Typography>
        <Stack direction="row" gap={1} justifyContent="flex-end">
          <Button onClick={onCancel} disabled={uploading}>Cancelar</Button>
          <Button variant="contained" onClick={() => void submit()} disabled={uploading}>
            {uploading ? 'Enviando…' : 'Enviar e processar'}
          </Button>
        </Stack>
      </Stack>
    </Paper>
  )
}

function ReviewBatch({ batchId, onBack, onConfirmed }: {
  batchId: string; onBack: () => void; onConfirmed: () => void
}) {
  const [batch, setBatch] = useState<EmployeeDocumentBatch | null>(null)
  const [documents, setDocuments] = useState<EmployeeDocument[]>([])
  const [employees, setEmployees] = useState<Employee[]>([])
  const [error, setError] = useState('')
  const [confirming, setConfirming] = useState(false)
  const [previewUrl, setPreviewUrl] = useState<string | null>(null)
  const [replacing, setReplacing] = useState<string | null>(null)
  const pollRef = useRef<ReturnType<typeof setInterval> | null>(null)

  const load = () => getBatch(batchId).then(detail => {
    setBatch(detail.batch); setDocuments(detail.documents)
  }).catch(() => setError('Não foi possível carregar o lote.'))

  useEffect(() => {
    void load()
    void listEmployees({}).then(setEmployees)
    pollRef.current = setInterval(() => { void load() }, 2500)
    return () => { if (pollRef.current) clearInterval(pollRef.current) }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [batchId])

  useEffect(() => {
    if (batch && batch.status !== 'Uploaded' && batch.status !== 'Processing' && pollRef.current) {
      clearInterval(pollRef.current); pollRef.current = null
    }
  }, [batch])

  const act = async (documentId: string, action: 'Assign' | 'Ignore' | 'Confirm', employeeId?: string) => {
    try { await updateDocumentAssociation(batchId, documentId, action, employeeId); await load() }
    catch (e) { setError(getErrorMessage(e)) }
  }

  const preview = async (documentId: string) => {
    try { setPreviewUrl(await previewDocumentUrl(batchId, documentId)) }
    catch { setError('Não foi possível abrir a pré-visualização.') }
  }

  const replace = async (documentId: string, file: File | undefined) => {
    if (!file) return
    setReplacing(documentId); setError('')
    try { await replaceDocumentFile(batchId, documentId, file); await load() }
    catch (e) { setError(getErrorMessage(e)) }
    finally { setReplacing(null) }
  }

  const confirmBatchAssociations = async () => {
    setConfirming(true); setError('')
    try { await confirmBatch(batchId); onConfirmed() }
    catch (e) { setError(getErrorMessage(e)) }
    finally { setConfirming(false) }
  }

  if (!batch) return <Alert severity="info">Carregando…</Alert>
  if (batch.status === 'Uploaded' || batch.status === 'Processing') return <Stack gap={1}><Alert severity="info">{batch.processingStage ?? 'Processando documentos'}{batch.totalItems ? ` — ${batch.processedItems} de ${batch.totalItems}` : ''}</Alert><LinearProgress variant={batch.progressPercentage === null ? 'indeterminate' : 'determinate'} value={batch.progressPercentage ?? undefined} /></Stack>
  if (batch.status === 'Uploaded' || batch.status === 'Processing')
    return <Alert severity="info">Processando documentos enviados… isso pode levar alguns instantes.</Alert>
  if (batch.status === 'Failed')
    return <Alert severity="error">{batch.failureReason ?? 'Não foi possível processar este lote.'}</Alert>

  const pendingCount = documents.filter(d => d.identificationStatus !== 'Confirmed' && d.identificationStatus !== 'Ignored').length
  const readyToConfirm = documents.length > 0 && pendingCount === 0

  return (
    <Stack gap={2}>
      <Typography variant="h3" fontSize={16} fontWeight={700}>
        Holerites — {competenceLabel(batch.competenceMonth, batch.competenceYear)}
      </Typography>
      <Typography color="text.secondary">
        {documents.length} documentos encontrados — {batch.identifiedCount} identificados com alta confiança,{' '}
        {batch.needsReviewCount} precisam revisão, {batch.unidentifiedCount} não identificados.
      </Typography>
      {error && <Alert severity="error" onClose={() => setError('')}>{error}</Alert>}
      <Stack gap={1.5}>
        {documents.map(document => (
          <Paper key={document.id} variant="outlined" sx={{ p: 2 }}>
            <Stack direction={{ xs: 'column', sm: 'row' }} justifyContent="space-between" alignItems={{ sm: 'flex-start' }} gap={2}>
              <Box minWidth={0} flex={1}>
                <Typography variant="body2" color="text.secondary" sx={{ overflowWrap: 'anywhere' }}>
                  {document.condominiumName ?? 'Condomínio não identificado'}
                </Typography>
                <Typography fontWeight={700} sx={{ overflowWrap: 'anywhere' }}>
                  {statusIcon[document.identificationStatus]} {document.employeeName ?? 'Não identificado'}
                </Typography>
                <Typography variant="body2" color="text.secondary" sx={{ overflowWrap: 'anywhere' }}>
                  Págs. {document.pageStart}–{document.pageEnd} · {confidenceLabel[document.identificationConfidence]}
                  {document.possibleDuplicate && ' · possível duplicidade com uma competência já confirmada'}
                </Typography>
              </Box>
              {document.identificationStatus !== 'Confirmed' && document.identificationStatus !== 'Ignored' && (
                <TextField select size="small" label="Funcionário" value={document.employeeId ?? ''}
                  sx={{ width: { xs: '100%', sm: 300 }, maxWidth: '100%', flexShrink: 0 }}
                  onChange={e => void act(document.id, 'Assign', e.target.value)}>
                  <MenuItem value="">Selecionar funcionário</MenuItem>
                  {employees.map(employee => <MenuItem key={employee.id} value={employee.id}>{employee.fullName}</MenuItem>)}
                </TextField>
              )}
            </Stack>
            <Stack direction="row" gap={1} useFlexGap flexWrap="wrap" alignItems="center"
              justifyContent={{ xs: 'flex-start', sm: 'flex-end' }}
              sx={{ borderTop: 1, borderColor: 'divider', mt: 2, pt: 1.5 }}>
                <Button size="small" onClick={() => void preview(document.id)}>Visualizar</Button>
                {document.identificationStatus !== 'Confirmed' && document.identificationStatus !== 'Ignored' && (
                  <>
                    <Button component="label" size="small" disabled={replacing === document.id}>
                      {replacing === document.id ? 'Substituindo…' : 'Substituir arquivo'}
                      <input hidden type="file" accept="application/pdf"
                        onChange={event => { void replace(document.id, event.target.files?.[0]); event.target.value = '' }} />
                    </Button>
                    <Button size="small" disabled={!document.employeeId} onClick={() => void act(document.id, 'Confirm')}>Confirmar</Button>
                    <Button size="small" color="inherit" onClick={() => void act(document.id, 'Ignore')}>Ignorar</Button>
                  </>
                )}
                {document.identificationStatus === 'Confirmed' && <Chip size="small" color="success" label="Confirmado" />}
                {document.identificationStatus === 'Ignored' && <Chip size="small" label="Ignorado" />}
            </Stack>
          </Paper>
        ))}
      </Stack>
      <Stack direction="row" gap={1} useFlexGap flexWrap="wrap" justifyContent="flex-end">
        <Button onClick={onBack}>Voltar</Button>
        <Button variant="contained" disabled={!readyToConfirm || confirming} onClick={() => void confirmBatchAssociations()}>
          Confirmar associações
        </Button>
      </Stack>
      <Dialog open={Boolean(previewUrl)} onClose={() => { if (previewUrl) URL.revokeObjectURL(previewUrl); setPreviewUrl(null) }}
        fullWidth maxWidth="md">
        <DialogTitle>Pré-visualização</DialogTitle>
        <DialogContent sx={{ height: '75vh' }}>
          {previewUrl && <iframe src={previewUrl} title="Pré-visualização do documento" width="100%" height="100%" style={{ border: 'none' }} />}
        </DialogContent>
        <DialogActions><Button onClick={() => { if (previewUrl) URL.revokeObjectURL(previewUrl); setPreviewUrl(null) }}>Fechar</Button></DialogActions>
      </Dialog>
    </Stack>
  )
}

function DistributionView({ batchId, onBack }: {
  batchId: string; onBack: () => void
}) {
  const [summary, setSummary] = useState<DistributionSummary | null>(null)
  const [deliveries, setDeliveries] = useState<EmployeeDocumentDelivery[]>([])
  const [confirmOpen, setConfirmOpen] = useState(false)
  const [sending, setSending] = useState(false)
  const [reopening, setReopening] = useState(false)
  const [error, setError] = useState('')
  const [rowMenu, setRowMenu] = useState<{ documentId: string; anchorEl: HTMLElement } | null>(null)
  const [confirmDeleteId, setConfirmDeleteId] = useState<string | null>(null)
  const [deleting, setDeleting] = useState(false)
  const [deletedIds, setDeletedIds] = useState<Set<string>>(new Set())

  const load = () => Promise.all([
    getDistributionSummary(batchId).then(setSummary),
    listDeliveries(batchId).then(setDeliveries),
  ]).catch(() => setError('Não foi possível carregar a distribuição.'))

  useEffect(() => {
    void load()
    const interval = setInterval(() => { void load() }, 4000)
    return () => clearInterval(interval)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [batchId])

  const send = async () => {
    setSending(true); setError(''); setConfirmOpen(false)
    try { await distributeBatch(batchId); await load() }
    catch (e) { setError(getErrorMessage(e)) }
    finally { setSending(false) }
  }

  const retry = async (documentId: string) => {
    try { await resendDocument(batchId, documentId); await load() }
    catch (e) { setError(getErrorMessage(e)) }
  }

  const reopen = async () => {
    setReopening(true); setError('')
    try { await reopenBatch(batchId); onBack() }
    catch (e) { setError(getErrorMessage(e)) }
    finally { setReopening(false) }
  }

  const removeDocument = async (documentId: string) => {
    setDeleting(true); setError(''); setConfirmDeleteId(null)
    try { await deleteDocument(batchId, documentId); setDeletedIds(current => new Set(current).add(documentId)) }
    catch (e) { setError(getErrorMessage(e)) }
    finally { setDeleting(false) }
  }

  if (!summary) return <Alert severity="info">Carregando…</Alert>

  return (
    <Stack gap={2}>
      <Typography variant="h3" fontSize={16} fontWeight={700}>Distribuição</Typography>
      {error && <Alert severity="error" onClose={() => setError('')}>{error}</Alert>}
      <Stack direction="row" gap={2} flexWrap="wrap">
        <SummaryStat label="Prontos" value={summary.ready} />
        <SummaryStat label="Sem telefone" value={summary.noPhone} />
        <SummaryStat label="Telefone inválido" value={summary.invalidPhone} />
        <SummaryStat label="Já enviados/enfileirados" value={summary.alreadyQueuedOrSent} />
      </Stack>
      {summary.ready > 0 && (
        <Box>
          <Button variant="contained" disabled={sending} onClick={() => setConfirmOpen(true)}>
            Enviar {summary.ready} holerite{summary.ready === 1 ? '' : 's'}
          </Button>
        </Box>
      )}
      {deliveries.length === 0 && <Box><Button disabled={reopening} onClick={() => void reopen()}>Revisar associações</Button></Box>}
      {deliveries.length > 0 && (
        <Paper variant="outlined" sx={{ overflowX: 'auto' }}>
          <Table>
            <TableHead><TableRow>{['Funcionário', 'Status', 'Enviado', 'Lido', ''].map(x => <TableCell key={x}>{x}</TableCell>)}</TableRow></TableHead>
            <TableBody>
              {deliveries.map(delivery => (
                <TableRow key={delivery.employeeDocumentId}>
                  <TableCell>{delivery.employeeName}</TableCell>
                  <TableCell><Chip size="small" label={delivery.status} color={
                    delivery.status === 'Failed' || delivery.status === 'PermanentlyFailed' ? 'error'
                      : delivery.status === 'Delivered' || delivery.status === 'Read' || delivery.status === 'Sent' ? 'success' : 'default'
                  } /></TableCell>
                  <TableCell>{delivery.sentAt ? new Date(delivery.sentAt).toLocaleString('pt-BR') : '—'}</TableCell>
                  <TableCell>{delivery.readAt ? 'Lido' : '—'}</TableCell>
                  <TableCell align="right">
                    {deletedIds.has(delivery.employeeDocumentId) ? (
                      <Chip size="small" label="Excluído" />
                    ) : (
                      <Stack direction="row" gap={0.5} justifyContent="flex-end">
                        {(delivery.status === 'Failed' || delivery.status === 'PermanentlyFailed') && (
                          <Button size="small" onClick={() => void retry(delivery.employeeDocumentId)}>Reenviar</Button>
                        )}
                        <IconButton size="small" aria-label={`Mais ações — ${delivery.employeeName}`}
                          onClick={event => setRowMenu({ documentId: delivery.employeeDocumentId, anchorEl: event.currentTarget })}>
                          <MoreVertRoundedIcon fontSize="small" />
                        </IconButton>
                      </Stack>
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Paper>
      )}
      <Box><Button onClick={onBack}>Voltar</Button></Box>
      <Menu open={Boolean(rowMenu)} anchorEl={rowMenu?.anchorEl} onClose={() => setRowMenu(null)}>
        <MenuItem disabled={rowMenu ? deliveries.find(d => d.employeeDocumentId === rowMenu.documentId)?.status === 'Pending'
          || deliveries.find(d => d.employeeDocumentId === rowMenu.documentId)?.status === 'Processing' : false}
          onClick={() => { const id = rowMenu!.documentId; setRowMenu(null); setConfirmDeleteId(id) }}>
          Excluir registro
        </MenuItem>
      </Menu>
      <Dialog open={confirmOpen} onClose={() => setConfirmOpen(false)}>
        <DialogTitle>Enviar {summary.ready} holerites via WhatsApp?</DialogTitle>
        <DialogContent>
          <DialogContentText>
            Cada funcionário receberá apenas o documento associado ao seu cadastro. Revise as associações antes de continuar.
          </DialogContentText>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setConfirmOpen(false)}>Cancelar</Button>
          <Button variant="contained" onClick={() => void send()}>Confirmar envio</Button>
        </DialogActions>
      </Dialog>
      <Dialog open={Boolean(confirmDeleteId)} onClose={() => setConfirmDeleteId(null)}>
        <DialogTitle>Excluir este holerite?</DialogTitle>
        <DialogContent>
          <DialogContentText>
            Ele será removido da gestão e não poderá ser reenviado. O histórico do envio já realizado será preservado.
          </DialogContentText>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setConfirmDeleteId(null)} disabled={deleting}>Cancelar</Button>
          <Button color="error" variant="contained" disabled={deleting} onClick={() => void removeDocument(confirmDeleteId!)}>Excluir</Button>
        </DialogActions>
      </Dialog>
    </Stack>
  )
}

function SummaryStat({ label, value }: { label: string; value: number }) {
  return (
    <Paper variant="outlined" sx={{ p: 2, minWidth: 120 }}>
      <Typography variant="h4" fontWeight={800}>{value}</Typography>
      <Typography color="text.secondary" variant="body2">{label}</Typography>
    </Paper>
  )
}
