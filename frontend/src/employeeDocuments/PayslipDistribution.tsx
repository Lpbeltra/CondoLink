import { useEffect, useRef, useState } from 'react'
import {
  Alert, Box, Button, Chip, Dialog, DialogActions, DialogContent, DialogContentText, DialogTitle,
  MenuItem, Paper, Stack, Table, TableBody, TableCell, TableHead, TableRow, TextField, Typography,
} from '@mui/material'
import { listEmployees, type Employee } from '../employees/api'
import { getErrorMessage } from '../services/api'
import {
  confirmBatch, distributeBatch, getBatch, getDistributionSummary, listBatches, listDeliveries,
  previewDocumentUrl, resendDocument, updateDocumentAssociation, uploadBatch,
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

export function PayslipDistribution({ condominiumId }: { condominiumId: string }) {
  const [view, setView] = useState<View>('history')
  const [batches, setBatches] = useState<EmployeeDocumentBatch[]>([])
  const [loadingHistory, setLoadingHistory] = useState(true)
  const [activeBatchId, setActiveBatchId] = useState<string | null>(null)
  const [error, setError] = useState('')

  const loadHistory = () => {
    setLoadingHistory(true)
    return listBatches(condominiumId).then(setBatches).catch(() => setError('Não foi possível carregar o histórico.'))
      .finally(() => setLoadingHistory(false))
  }
  // Intentionally reloads only when the condominium changes.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  useEffect(() => { void loadHistory() }, [condominiumId])

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
        <UploadBatch condominiumId={condominiumId}
          onCancel={() => setView('history')}
          onUploaded={batchId => { setActiveBatchId(batchId); setView('review') }} />
      )}
      {view === 'review' && activeBatchId && (
        <ReviewBatch condominiumId={condominiumId} batchId={activeBatchId}
          onBack={() => { setView('history'); void loadHistory() }}
          onConfirmed={() => setView('distribution')} />
      )}
      {view === 'distribution' && activeBatchId && (
        <DistributionView condominiumId={condominiumId} batchId={activeBatchId}
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

function UploadBatch({ condominiumId, onCancel, onUploaded }: {
  condominiumId: string; onCancel: () => void; onUploaded: (batchId: string) => void
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
      const result = await uploadBatch(condominiumId, files, month, year)
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

function ReviewBatch({ condominiumId, batchId, onBack, onConfirmed }: {
  condominiumId: string; batchId: string; onBack: () => void; onConfirmed: () => void
}) {
  const [batch, setBatch] = useState<EmployeeDocumentBatch | null>(null)
  const [documents, setDocuments] = useState<EmployeeDocument[]>([])
  const [employees, setEmployees] = useState<Employee[]>([])
  const [error, setError] = useState('')
  const [confirming, setConfirming] = useState(false)
  const [previewUrl, setPreviewUrl] = useState<string | null>(null)
  const pollRef = useRef<ReturnType<typeof setInterval> | null>(null)

  const load = () => getBatch(condominiumId, batchId).then(detail => {
    setBatch(detail.batch); setDocuments(detail.documents)
  }).catch(() => setError('Não foi possível carregar o lote.'))

  useEffect(() => {
    void load()
    void listEmployees(condominiumId, {}).then(setEmployees)
    pollRef.current = setInterval(() => { void load() }, 2500)
    return () => { if (pollRef.current) clearInterval(pollRef.current) }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [condominiumId, batchId])

  useEffect(() => {
    if (batch && batch.status !== 'Uploaded' && batch.status !== 'Processing' && pollRef.current) {
      clearInterval(pollRef.current); pollRef.current = null
    }
  }, [batch])

  const act = async (documentId: string, action: 'Assign' | 'Ignore' | 'Confirm', employeeId?: string) => {
    try { await updateDocumentAssociation(condominiumId, documentId, action, employeeId); await load() }
    catch (e) { setError(getErrorMessage(e)) }
  }

  const preview = async (documentId: string) => {
    try { setPreviewUrl(await previewDocumentUrl(condominiumId, documentId)) }
    catch { setError('Não foi possível abrir a pré-visualização.') }
  }

  const confirmBatchAssociations = async () => {
    setConfirming(true); setError('')
    try { await confirmBatch(condominiumId, batchId); onConfirmed() }
    catch (e) { setError(getErrorMessage(e)) }
    finally { setConfirming(false) }
  }

  if (!batch) return <Alert severity="info">Carregando…</Alert>
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
            <Stack direction={{ xs: 'column', sm: 'row' }} justifyContent="space-between" gap={2}>
              <Box>
                <Typography fontWeight={700}>
                  {statusIcon[document.identificationStatus]} {document.employeeName ?? 'Não identificado'}
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  Págs. {document.pageStart}–{document.pageEnd} · {confidenceLabel[document.identificationConfidence]}
                  {document.possibleDuplicate && ' · possível duplicidade com uma competência já confirmada'}
                </Typography>
              </Box>
              <Stack direction="row" gap={1} flexWrap="wrap" alignItems="center">
                {document.identificationStatus !== 'Confirmed' && document.identificationStatus !== 'Ignored' && (
                  <TextField select size="small" label="Funcionário" value={document.employeeId ?? ''}
                    sx={{ minWidth: 200 }}
                    onChange={e => void act(document.id, 'Assign', e.target.value)}>
                    <MenuItem value="">Selecionar funcionário</MenuItem>
                    {employees.map(employee => <MenuItem key={employee.id} value={employee.id}>{employee.fullName}</MenuItem>)}
                  </TextField>
                )}
                <Button size="small" onClick={() => void preview(document.id)}>Visualizar</Button>
                {document.identificationStatus !== 'Confirmed' && document.identificationStatus !== 'Ignored' && (
                  <>
                    <Button size="small" disabled={!document.employeeId} onClick={() => void act(document.id, 'Confirm')}>Confirmar</Button>
                    <Button size="small" color="inherit" onClick={() => void act(document.id, 'Ignore')}>Ignorar</Button>
                  </>
                )}
                {document.identificationStatus === 'Confirmed' && <Chip size="small" color="success" label="Confirmado" />}
                {document.identificationStatus === 'Ignored' && <Chip size="small" label="Ignorado" />}
              </Stack>
            </Stack>
          </Paper>
        ))}
      </Stack>
      <Stack direction="row" gap={1} justifyContent="flex-end">
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

function DistributionView({ condominiumId, batchId, onBack }: {
  condominiumId: string; batchId: string; onBack: () => void
}) {
  const [summary, setSummary] = useState<DistributionSummary | null>(null)
  const [deliveries, setDeliveries] = useState<EmployeeDocumentDelivery[]>([])
  const [confirmOpen, setConfirmOpen] = useState(false)
  const [sending, setSending] = useState(false)
  const [error, setError] = useState('')

  const load = () => Promise.all([
    getDistributionSummary(condominiumId, batchId).then(setSummary),
    listDeliveries(condominiumId, batchId).then(setDeliveries),
  ]).catch(() => setError('Não foi possível carregar a distribuição.'))

  useEffect(() => {
    void load()
    const interval = setInterval(() => { void load() }, 4000)
    return () => clearInterval(interval)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [condominiumId, batchId])

  const send = async () => {
    setSending(true); setError(''); setConfirmOpen(false)
    try { await distributeBatch(condominiumId, batchId); await load() }
    catch (e) { setError(getErrorMessage(e)) }
    finally { setSending(false) }
  }

  const retry = async (documentId: string) => {
    try { await resendDocument(condominiumId, documentId); await load() }
    catch (e) { setError(getErrorMessage(e)) }
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
                    {(delivery.status === 'Failed' || delivery.status === 'PermanentlyFailed') && (
                      <Button size="small" onClick={() => void retry(delivery.employeeDocumentId)}>Reenviar</Button>
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Paper>
      )}
      <Box><Button onClick={onBack}>Voltar</Button></Box>
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
