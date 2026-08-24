import { useEffect, useState } from 'react'
import EditRoundedIcon from '@mui/icons-material/EditRounded'
import { Alert, Box, Button, Card, CardActions, CardContent, Chip, Dialog, DialogActions, DialogContent, DialogTitle, Skeleton, Stack, TextField, Typography } from '@mui/material'
import { PageContainer } from '../../components/PageContainer'
import { formatDateTime } from '../../requests/presentation'
import { getErrorMessage } from '../../services/api'
import { listOperationalMessages, previewMessage, restoreOperationalMessage, updateOperationalMessage, type OperationalMessageTemplate } from '../messages'

function updateSummary(template: OperationalMessageTemplate) {
  if (!template.isOverride || !template.updatedAt) return null
  const who = template.updatedByUserId ? ` · por ${template.updatedByUserId.slice(0, 8)}` : ''
  return `Personalizada · atualizada em ${formatDateTime(template.updatedAt)}${who}`
}

function TemplateCard({ template, onEdit }: { template: OperationalMessageTemplate; onEdit: () => void }) {
  const summary = updateSummary(template)
  return (
    <Card elevation={0}>
      <CardContent>
        <Stack direction={{ xs: 'column', sm: 'row' }} justifyContent="space-between" gap={1}>
          <Box>
            <Typography variant="h2">{template.title}</Typography>
            <Typography color="text.secondary" mt={1}>{template.description}</Typography>
            {summary && <Typography variant="caption" color="text.secondary">{summary}</Typography>}
          </Box>
          <Stack direction="row" gap={1} alignItems="flex-start">
            <Chip label={template.modeLabel} color="info" variant="outlined" />
            {template.isOverride && <Chip label="Personalizada" color="primary" />}
          </Stack>
        </Stack>
      </CardContent>
      <CardActions>
        <Button startIcon={<EditRoundedIcon />} onClick={onEdit}>Editar</Button>
      </CardActions>
    </Card>
  )
}

function TemplateEditDialog({
  editing,
  prefix,
  suffix,
  saving,
  feedback,
  actionError,
  onPrefixChange,
  onSuffixChange,
  onClose,
  onSave,
  onRestore,
}: {
  editing: OperationalMessageTemplate | null
  prefix: string
  suffix: string
  saving: boolean
  feedback: string
  actionError: string
  onPrefixChange: (value: string) => void
  onSuffixChange: (value: string) => void
  onClose: () => void
  onSave: () => void
  onRestore: () => void
}) {
  const draft = editing ? { ...editing, prefix, suffix } : null
  return (
    <Dialog open={Boolean(editing)} onClose={() => !saving && onClose()} fullWidth maxWidth="md">
      <DialogTitle>{editing?.title}</DialogTitle>
      <DialogContent>
        <Stack gap={2} pt={1}>
          {feedback && <Alert severity="success">{feedback}</Alert>}
          {actionError && <Alert severity="error">{actionError}</Alert>}
          <Alert severity="info">
            <b>{editing?.modeLabel}.</b> A edição afeta somente o texto dentro da janela de 24 horas. O conteúdo fora
            dela depende do template Meta{' '}
            <b>
              {editing?.metaTemplateName || 'não configurado'}
              {editing?.metaTemplateLanguage ? ` · ${editing.metaTemplateLanguage}` : ''}
            </b>
            , gerenciado externamente.
            {editing?.metaQuickReplies?.length ? ` Botões: ${editing.metaQuickReplies.join(' · ')}.` : ''}
          </Alert>
          {editing && updateSummary(editing) && (
            <Typography variant="caption" color="text.secondary">{updateSummary(editing)}</Typography>
          )}
          <TextField
            label="Antes da mensagem do síndico"
            multiline
            minRows={4}
            value={prefix}
            onChange={(event) => onPrefixChange(event.target.value)}
            inputProps={{ maxLength: editing?.partMaximumLength }}
            helperText={`${prefix.length}/${editing?.partMaximumLength ?? 1200}`}
          />
          <TextField
            label="Conteúdo dinâmico"
            value="{MensagemDoSindico}"
            disabled
            helperText="Conteúdo escrito/aprovado pela administração; não é configurável aqui."
          />
          <TextField
            label="Depois da mensagem do síndico"
            multiline
            minRows={4}
            value={suffix}
            onChange={(event) => onSuffixChange(event.target.value)}
            inputProps={{ maxLength: editing?.partMaximumLength }}
            helperText={`${suffix.length}/${editing?.partMaximumLength ?? 1200}`}
          />
          {editing?.structuralSuffix && (
            <TextField
              label="Parte estrutural — somente leitura"
              multiline
              value={editing.structuralSuffix}
              disabled
              helperText="Necessária para preservar o fluxo do atendimento."
            />
          )}
          {draft && (
            <Card variant="outlined">
              <CardContent>
                <Typography variant="h3" mb={2}>Prévia com dados fictícios</Typography>
                <Typography component="pre" sx={{ whiteSpace: 'pre-wrap', font: 'inherit' }}>
                  {previewMessage(draft)}
                </Typography>
                <Typography variant="caption" color="text.secondary">
                  Maria · Residencial Exemplo · Aguardamos retorno da empresa responsável.
                </Typography>
              </CardContent>
            </Card>
          )}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button color="warning" onClick={onRestore} disabled={saving || !editing?.isOverride}>
          Restaurar padrão
        </Button>
        <Box flex={1} />
        <Button onClick={onClose} disabled={saving}>Fechar</Button>
        <Button variant="contained" onClick={onSave} disabled={saving}>Salvar</Button>
      </DialogActions>
    </Dialog>
  )
}

export function OverwatchMessagesPage() {
  const [templates, setTemplates] = useState<OperationalMessageTemplate[]>([])
  const [loading, setLoading] = useState(true)
  const [listError, setListError] = useState('')
  const [editing, setEditing] = useState<OperationalMessageTemplate | null>(null)
  const [prefix, setPrefix] = useState('')
  const [suffix, setSuffix] = useState('')
  const [saving, setSaving] = useState(false)
  const [feedback, setFeedback] = useState('')
  const [actionError, setActionError] = useState('')

  useEffect(() => {
    listOperationalMessages()
      .then(setTemplates)
      .catch((error) => setListError(getErrorMessage(error)))
      .finally(() => setLoading(false))
  }, [])

  const open = (template: OperationalMessageTemplate) => {
    setEditing(template)
    setPrefix(template.prefix)
    setSuffix(template.suffix)
    setActionError('')
    setFeedback('')
  }
  const close = () => setEditing(null)
  const replace = (updated: OperationalMessageTemplate) => {
    setTemplates((current) => current.map((item) => (item.key === updated.key ? updated : item)))
    setEditing(updated)
    setPrefix(updated.prefix)
    setSuffix(updated.suffix)
  }
  const save = async () => {
    if (!editing) return
    setSaving(true)
    setActionError('')
    try {
      replace(await updateOperationalMessage(editing.key, prefix, suffix))
      setFeedback('Mensagem salva com sucesso.')
    } catch (error) {
      setActionError(getErrorMessage(error))
    } finally {
      setSaving(false)
    }
  }
  const restore = async () => {
    if (!editing) return
    setSaving(true)
    setActionError('')
    try {
      replace(await restoreOperationalMessage(editing.key))
      setFeedback('Padrão oficial restaurado.')
    } catch (error) {
      setActionError(getErrorMessage(error))
    } finally {
      setSaving(false)
    }
  }

  return (
    <PageContainer>
      <Typography variant="h1">Mensagens</Typography>
      <Typography color="text.secondary" mt={1}>
        Molduras globais das mensagens operacionais enviadas aos moradores.
      </Typography>
      {listError && <Alert severity="error" sx={{ mt: 2 }}>{listError}</Alert>}
      {loading ? (
        <Skeleton variant="rounded" height={180} sx={{ mt: 3 }} />
      ) : (
        <Stack gap={2} mt={3}>
          {templates.map((template) => (
            <TemplateCard key={template.key} template={template} onEdit={() => open(template)} />
          ))}
        </Stack>
      )}
      <TemplateEditDialog
        editing={editing}
        prefix={prefix}
        suffix={suffix}
        saving={saving}
        feedback={feedback}
        actionError={actionError}
        onPrefixChange={setPrefix}
        onSuffixChange={setSuffix}
        onClose={close}
        onSave={() => void save()}
        onRestore={() => void restore()}
      />
    </PageContainer>
  )
}
