import { useEffect, useState } from 'react'
import EditRoundedIcon from '@mui/icons-material/EditRounded'
import { Alert, Box, Button, Card, CardActions, CardContent, Chip, Dialog, DialogActions, DialogContent, DialogTitle, Skeleton, Stack, TextField, Typography } from '@mui/material'
import { PageContainer } from '../../components/PageContainer'
import { listOperationalMessages, previewMessage, restoreOperationalMessage, updateOperationalMessage, type OperationalMessageTemplate } from '../messages'

export function OverwatchMessagesPage() {
  const [templates, setTemplates] = useState<OperationalMessageTemplate[]>([])
  const [loading, setLoading] = useState(true); const [error, setError] = useState('')
  const [editing, setEditing] = useState<OperationalMessageTemplate | null>(null)
  const [prefix, setPrefix] = useState(''); const [suffix, setSuffix] = useState('')
  const [saving, setSaving] = useState(false); const [feedback, setFeedback] = useState('')
  useEffect(() => { listOperationalMessages().then(setTemplates).catch(() => setError('Não foi possível carregar as mensagens.')).finally(() => setLoading(false)) }, [])
  const open = (template: OperationalMessageTemplate) => { setEditing(template); setPrefix(template.prefix); setSuffix(template.suffix); setError(''); setFeedback('') }
  const replace = (updated: OperationalMessageTemplate) => { setTemplates((current) => current.map((item) => item.key === updated.key ? updated : item)); setEditing(updated); setPrefix(updated.prefix); setSuffix(updated.suffix) }
  const save = async () => { if (!editing) return; setSaving(true); setError(''); try { replace(await updateOperationalMessage(editing.key, prefix, suffix)); setFeedback('Mensagem salva com sucesso.') } catch { setError('Não foi possível salvar a mensagem.') } finally { setSaving(false) } }
  const restore = async () => { if (!editing) return; setSaving(true); setError(''); try { replace(await restoreOperationalMessage(editing.key)); setFeedback('Padrão oficial restaurado.') } catch { setError('Não foi possível restaurar o padrão.') } finally { setSaving(false) } }
  const draft = editing ? { ...editing, prefix, suffix } : null
  return <PageContainer><Typography variant="h1">Mensagens</Typography><Typography color="text.secondary" mt={1}>Molduras globais das mensagens operacionais enviadas aos moradores.</Typography>
    {error && !editing && <Alert severity="error" sx={{ mt: 2 }}>{error}</Alert>}
    {loading ? <Skeleton variant="rounded" height={180} sx={{ mt: 3 }} /> : <Stack gap={2} mt={3}>{templates.map((template) => <Card key={template.key} elevation={0}><CardContent><Stack direction={{ xs: 'column', sm: 'row' }} justifyContent="space-between" gap={1}><Box><Typography variant="h2">{template.title}</Typography><Typography color="text.secondary" mt={1}>{template.description}</Typography></Box><Stack direction="row" gap={1} alignItems="flex-start"><Chip label={template.modeLabel} color="info" variant="outlined" />{template.isOverride && <Chip label="Personalizada" color="primary" />}</Stack></Stack></CardContent><CardActions><Button startIcon={<EditRoundedIcon />} onClick={() => open(template)}>Editar</Button></CardActions></Card>)}</Stack>}
    <Dialog open={Boolean(editing)} onClose={() => !saving && setEditing(null)} fullWidth maxWidth="md"><DialogTitle>{editing?.title}</DialogTitle><DialogContent><Stack gap={2} pt={1}>
      {feedback && <Alert severity="success">{feedback}</Alert>}{error && <Alert severity="error">{error}</Alert>}
      <Alert severity="info"><b>{editing?.modeLabel}.</b> A edição afeta somente o texto dentro da janela de 24 horas. O conteúdo fora dela depende do template Meta <b>{editing?.metaTemplateName || 'não configurado'}{editing?.metaTemplateLanguage ? ` · ${editing.metaTemplateLanguage}` : ''}</b>, gerenciado externamente.</Alert>
      <TextField label="Antes da mensagem do síndico" multiline minRows={4} value={prefix} onChange={(event) => setPrefix(event.target.value)} inputProps={{ maxLength: editing?.partMaximumLength }} helperText={`${prefix.length}/${editing?.partMaximumLength ?? 1200}`} />
      <TextField label="Conteúdo dinâmico" value="{MensagemDoSindico}" disabled helperText="Conteúdo escrito/aprovado pela administração; não é configurável aqui." />
      <TextField label="Depois da mensagem do síndico" multiline minRows={4} value={suffix} onChange={(event) => setSuffix(event.target.value)} inputProps={{ maxLength: editing?.partMaximumLength }} helperText={`${suffix.length}/${editing?.partMaximumLength ?? 1200}`} />
      {editing?.structuralSuffix && <TextField label="Parte estrutural — somente leitura" multiline value={editing.structuralSuffix} disabled helperText="Necessária para preservar o fluxo do atendimento." />}
      {draft && <Card variant="outlined"><CardContent><Typography variant="h3" mb={2}>Prévia com dados fictícios</Typography><Typography component="pre" sx={{ whiteSpace: 'pre-wrap', font: 'inherit' }}>{previewMessage(draft)}</Typography><Typography variant="caption" color="text.secondary">Maria · Residencial Exemplo · Aguardamos retorno da empresa responsável.</Typography></CardContent></Card>}
    </Stack></DialogContent><DialogActions><Button color="warning" onClick={() => void restore()} disabled={saving || !editing?.isOverride}>Restaurar padrão</Button><Box flex={1} /><Button onClick={() => setEditing(null)} disabled={saving}>Fechar</Button><Button variant="contained" onClick={() => void save()} disabled={saving}>Salvar</Button></DialogActions></Dialog>
  </PageContainer>
}
