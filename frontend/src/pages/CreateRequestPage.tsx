import { useEffect, useRef, useState, type FormEvent } from 'react'
import ArrowBackRoundedIcon from '@mui/icons-material/ArrowBackRounded'
import SendRoundedIcon from '@mui/icons-material/SendRounded'
import { Alert, Box, Button, Card, CardContent, CircularProgress, FormControl, FormHelperText, InputLabel, MenuItem, Select, Skeleton, Stack, TextField, Typography } from '@mui/material'
import { useNavigate } from 'react-router-dom'
import { EmptyState } from '../components/EmptyState'
import { PageContainer } from '../components/PageContainer'
import { useCondominium } from '../condominiums/CondominiumContext'
import { createRequest, listCategories } from '../requests/api'
import { getRequestError } from '../requests/presentation'
import type { Category } from '../requests/types'

export function CreateRequestPage() {
  const navigate = useNavigate()
  const { currentCondominium } = useCondominium()
  const condominiumId = currentCondominium!.condominium.id
  const [categories, setCategories] = useState<Category[]>([])
  const [loadedCondominiumId, setLoadedCondominiumId] = useState('')
  const [categoryId, setCategoryId] = useState('')
  const [title, setTitle] = useState('')
  const [description, setDescription] = useState('')
  const [isLoading, setIsLoading] = useState(true)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [error, setError] = useState('')
  const [submitted, setSubmitted] = useState(false)
  const [reloadKey, setReloadKey] = useState(0)
  const activeLoad = useRef(0)

  useEffect(() => {
    const version = ++activeLoad.current
    setIsLoading(true); setCategories([]); setCategoryId(''); setLoadedCondominiumId(''); setError('')
    listCategories(condominiumId).then((data) => { if (version === activeLoad.current) { setCategories(data); setLoadedCondominiumId(condominiumId) } })
      .catch((requestError) => { if (version === activeLoad.current) setError(getRequestError(requestError, 'Não foi possível carregar as categorias.')) })
      .finally(() => { if (version === activeLoad.current) setIsLoading(false) })
  }, [condominiumId, reloadKey])

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault(); setSubmitted(true)
    const cleanTitle = title.trim(); const cleanDescription = description.trim()
    if (!categoryId || !cleanTitle || !cleanDescription || cleanTitle.length > 200 || cleanDescription.length > 4000 || isSubmitting) return
    setIsSubmitting(true); setError('')
    try {
      const created = await createRequest(condominiumId, { categoryId, title: cleanTitle, description: cleanDescription })
      navigate(`/requests/${created.id}`, { replace: true, state: { created: true } })
    } catch (requestError) { setError(getRequestError(requestError, 'Não foi possível abrir a solicitação. Atualize a página ou confira o condomínio selecionado.')) }
    finally { setIsSubmitting(false) }
  }

  const ready = loadedCondominiumId === condominiumId
  return (
    <PageContainer maxWidth={820}>
      <Button startIcon={<ArrowBackRoundedIcon />} color="inherit" onClick={() => navigate('/requests')} sx={{ mb: 2 }}>Voltar</Button>
      <Typography variant="h1">Nova solicitação</Typography>
      <Typography color="text.secondary" mt={.75} mb={3}>Conte à administração como ela pode ajudar.</Typography>
      {isLoading || !ready ? <Skeleton variant="rounded" height={420} /> : error && categories.length === 0 ? <EmptyState title="Não foi possível carregar as categorias" description={error} action={<Button variant="contained" onClick={() => setReloadKey((value) => value + 1)}>Tentar novamente</Button>} /> : categories.length === 0 ? <EmptyState title="Nenhuma categoria disponível" description="Ainda não é possível abrir uma solicitação neste condomínio. Entre em contato com a administração." /> : (
        <Card elevation={0}><CardContent sx={{ p: { xs: 2.5, sm: 4 } }}>
          <Box component="form" onSubmit={handleSubmit} noValidate>
            <Stack spacing={2.5}>
              {error && <Alert severity="error">{error}</Alert>}
              <FormControl required error={submitted && !categoryId}>
                <InputLabel id="category-label">Categoria</InputLabel>
                <Select labelId="category-label" label="Categoria" value={categoryId} onChange={(event) => setCategoryId(event.target.value)} disabled={isSubmitting}>
                  {categories.map((category) => <MenuItem key={category.id} value={category.id}>{category.name}</MenuItem>)}
                </Select>
                {submitted && !categoryId && <FormHelperText>Selecione uma categoria.</FormHelperText>}
              </FormControl>
              <TextField required label="Título" value={title} onChange={(event) => setTitle(event.target.value)} inputProps={{ maxLength: 200 }} error={submitted && !title.trim()} helperText={submitted && !title.trim() ? 'Informe um título.' : `${title.length}/200`} disabled={isSubmitting} />
              <TextField required multiline minRows={6} maxRows={14} label="Descrição" value={description} onChange={(event) => setDescription(event.target.value)} inputProps={{ maxLength: 4000 }} error={submitted && !description.trim()} helperText={submitted && !description.trim() ? 'Descreva o que aconteceu.' : `${description.length}/4000`} disabled={isSubmitting} />
              <Typography color="text.secondary" fontSize=".8rem">A seleção de unidade será adicionada futuramente. Esta solicitação será aberta sem unidade relacionada.</Typography>
              <Box display="flex" justifyContent="flex-end"><Button type="submit" variant="contained" size="large" disabled={isSubmitting || categories.length === 0} startIcon={isSubmitting ? <CircularProgress size={18} color="inherit" /> : <SendRoundedIcon />}>{isSubmitting ? 'Abrindo…' : 'Abrir solicitação'}</Button></Box>
            </Stack>
          </Box>
        </CardContent></Card>
      )}
    </PageContainer>
  )
}
