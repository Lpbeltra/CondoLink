import { useEffect, useRef, useState, type FormEvent } from 'react'
import ArrowBackRoundedIcon from '@mui/icons-material/ArrowBackRounded'
import SendRoundedIcon from '@mui/icons-material/SendRounded'
import { Alert, Box, Button, Card, CardContent, CircularProgress, FormControl, FormHelperText, InputLabel, MenuItem, Select, Skeleton, Stack, TextField, Typography } from '@mui/material'
import { useNavigate } from 'react-router-dom'
import { EmptyState } from '../components/EmptyState'
import { PageContainer } from '../components/PageContainer'
import { useCondominium } from '../condominiums/CondominiumContext'
import { createRequest, listCategories, listMyRequestUnits } from '../requests/api'
import { getRequestError } from '../requests/presentation'
import type { Category, RequestUnitOption } from '../requests/types'

export function CreateRequestPage() {
  const navigate = useNavigate()
  const { currentCondominium } = useCondominium()
  const condominiumId = currentCondominium!.condominium.id
  const [categories, setCategories] = useState<Category[]>([])
  const [units, setUnits] = useState<RequestUnitOption[]>([])
  const [loadedCondominiumId, setLoadedCondominiumId] = useState('')
  const [categoryId, setCategoryId] = useState('')
  const [targetUnitId, setTargetUnitId] = useState('')
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
    setIsLoading(true); setCategories([]); setUnits([]); setCategoryId(''); setTargetUnitId(''); setLoadedCondominiumId(''); setError('')
    Promise.all([listCategories(condominiumId), listMyRequestUnits(condominiumId)]).then(([categoryData, unitData]) => { if (version === activeLoad.current) { setCategories(categoryData); setUnits(unitData); setTargetUnitId(unitData.length === 1 ? unitData[0].id : ''); setLoadedCondominiumId(condominiumId) } })
      .catch((requestError) => { if (version === activeLoad.current) setError(getRequestError(requestError, 'Não foi possível carregar as categorias.')) })
      .finally(() => { if (version === activeLoad.current) setIsLoading(false) })
  }, [condominiumId, reloadKey])

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault(); setSubmitted(true)
    const cleanTitle = title.trim(); const cleanDescription = description.trim()
    if (!categoryId || (units.length > 1 && !targetUnitId) || !cleanTitle || !cleanDescription || cleanTitle.length > 200 || cleanDescription.length > 4000 || isSubmitting) return
    setIsSubmitting(true); setError('')
    try {
      const created = await createRequest(condominiumId, { categoryId, targetUnitId: targetUnitId || null, title: cleanTitle, description: cleanDescription })
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
              {units.length > 1 ? <FormControl required error={submitted && !targetUnitId}><InputLabel id="unit-label">Unidade</InputLabel><Select labelId="unit-label" label="Unidade" value={targetUnitId} onChange={(event) => setTargetUnitId(event.target.value)} disabled={isSubmitting}>{units.map((unit) => <MenuItem key={unit.id} value={unit.id}>{unit.block ? `Bloco ${unit.block} · ` : ''}{unit.identifier}</MenuItem>)}</Select>{submitted && !targetUnitId && <FormHelperText>Selecione a unidade relacionada.</FormHelperText>}</FormControl> : units.length === 1 ? <Alert severity="info">Unidade relacionada automaticamente: {units[0].block ? `Bloco ${units[0].block} · ` : ''}{units[0].identifier}.</Alert> : <Typography color="text.secondary" fontSize=".8rem">Você não possui unidade ativa neste condomínio. A solicitação será aberta sem unidade relacionada.</Typography>}
              <TextField required label="Título" value={title} onChange={(event) => setTitle(event.target.value)} inputProps={{ maxLength: 200 }} error={submitted && !title.trim()} helperText={submitted && !title.trim() ? 'Informe um título.' : `${title.length}/200`} disabled={isSubmitting} />
              <TextField required multiline minRows={6} maxRows={14} label="Descrição" value={description} onChange={(event) => setDescription(event.target.value)} inputProps={{ maxLength: 4000 }} error={submitted && !description.trim()} helperText={submitted && !description.trim() ? 'Descreva o que aconteceu.' : `${description.length}/4000`} disabled={isSubmitting} />
              <Box display="flex" justifyContent="flex-end"><Button type="submit" variant="contained" size="large" disabled={isSubmitting || categories.length === 0} startIcon={isSubmitting ? <CircularProgress size={18} color="inherit" /> : <SendRoundedIcon />}>{isSubmitting ? 'Abrindo…' : 'Abrir solicitação'}</Button></Box>
            </Stack>
          </Box>
        </CardContent></Card>
      )}
    </PageContainer>
  )
}
