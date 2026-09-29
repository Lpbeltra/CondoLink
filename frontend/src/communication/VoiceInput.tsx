import { useCallback, useEffect, useRef, useState } from 'react'
import MicRoundedIcon from '@mui/icons-material/MicRounded'
import StopRoundedIcon from '@mui/icons-material/StopRounded'
import { Alert, Box, Button, CircularProgress, IconButton, InputAdornment, Stack, Tooltip, Typography } from '@mui/material'
import { api, getErrorMessage } from '../services/api'

const MAX_SECONDS = 120
const MAX_BYTES = 10 * 1024 * 1024
let recordingOwner: symbol | null = null
type Phase = 'idle' | 'requesting' | 'recording' | 'transcribing'
interface Props {
  endpoint: string
  value: string
  onChange: (text: string) => void
  onBusyChange?: (busy: boolean) => void
  maxLength: number
  disabled?: boolean
  active?: boolean
  inline?: boolean
}

export function VoiceInput({ endpoint, value, onChange, onBusyChange, maxLength, disabled = false, active = true, inline = false }: Props) {
  const [phase, setPhase] = useState<Phase>('idle')
  const [seconds, setSeconds] = useState(0)
  const [error, setError] = useState('')
  const mounted = useRef(false)
  const generation = useRef(0)
  const owner = useRef<symbol | null>(null)
  const recorder = useRef<MediaRecorder | null>(null)
  const stream = useRef<MediaStream | null>(null)
  const timer = useRef<ReturnType<typeof setInterval> | null>(null)
  const abort = useRef<AbortController | null>(null)
  const latest = useRef({ value, onChange, maxLength })
  latest.current = { value, onChange, maxLength }

  const release = useCallback(() => {
    if (timer.current) clearInterval(timer.current)
    timer.current = null
    stream.current?.getTracks().forEach(track => track.stop())
    stream.current = null
    if (recordingOwner === owner.current) recordingOwner = null
    owner.current = null
  }, [])
  const cancel = useCallback(() => {
    generation.current++
    abort.current?.abort()
    abort.current = null
    const capture = recorder.current
    recorder.current = null
    if (capture) {
      capture.onstop = null
      capture.ondataavailable = null
      capture.onerror = null
      if (capture.state !== 'inactive') capture.stop()
    }
    release()
    if (mounted.current) setPhase('idle')
  }, [release])

  useEffect(() => {
    mounted.current = true
    const background = () => { if (document.visibilityState === 'hidden') cancel() }
    const leave = () => cancel()
    document.addEventListener('visibilitychange', background)
    window.addEventListener('pagehide', leave)
    return () => {
      mounted.current = false
      cancel()
      document.removeEventListener('visibilitychange', background)
      window.removeEventListener('pagehide', leave)
    }
  }, [cancel])
  useEffect(() => { cancel() }, [cancel, endpoint])
  useEffect(() => { if (!active) cancel() }, [active, cancel])
  useEffect(() => { onBusyChange?.(phase !== 'idle'); return () => onBusyChange?.(false) }, [onBusyChange, phase])

  const start = async () => {
    if (disabled || !active || owner.current) return
    setError('')
    if (recordingOwner) { setError('Já existe uma gravação em andamento. Conclua ou cancele antes de iniciar outra.'); return }
    if (window.isSecureContext === false || !navigator.mediaDevices?.getUserMedia || typeof MediaRecorder === 'undefined') {
      setError('Este navegador não permite gravar aqui. Use HTTPS e um navegador com suporte ao microfone.'); return
    }
    const mimeType = ['audio/webm;codecs=opus', 'audio/webm', 'audio/mp4', 'audio/ogg;codecs=opus']
      .find(type => MediaRecorder.isTypeSupported(type))
    if (!mimeType) { setError('Este navegador não oferece um formato de gravação compatível. Você pode escrever a mensagem.'); return }
    const version = ++generation.current
    owner.current = Symbol('voice-input')
    recordingOwner = owner.current
    setPhase('requesting')
    try {
      const media = await navigator.mediaDevices.getUserMedia({ audio: true })
      if (version !== generation.current || !mounted.current) { media.getTracks().forEach(track => track.stop()); return }
      stream.current = media
      const capture = new MediaRecorder(media, { mimeType, audioBitsPerSecond: 128_000 })
      recorder.current = capture
      const chunks: Blob[] = []
      let bytes = 0
      capture.ondataavailable = event => {
        if (version !== generation.current || !event.data.size) return
        bytes += event.data.size
        if (bytes > MAX_BYTES) { cancel(); setError('A gravação excedeu 10 MB. Grave uma mensagem mais curta.'); return }
        chunks.push(event.data)
      }
      capture.onerror = () => { cancel(); setError('A gravação foi interrompida. Tente novamente ou escreva a mensagem.') }
      capture.onstop = async () => {
        if (version !== generation.current || !mounted.current) return
        release()
        recorder.current = null
        const audio = new Blob(chunks, { type: capture.mimeType || mimeType })
        if (!audio.size) { setPhase('idle'); setError('Nenhum áudio foi gravado. Tente novamente.'); return }
        setPhase('transcribing')
        const controller = new AbortController()
        abort.current = controller
        const format = audio.type.split(';')[0]
        const extension = format === 'audio/mp4' ? 'm4a' : format === 'audio/ogg' ? 'ogg' : 'webm'
        const form = new FormData()
        form.append('audio', audio, `voice.${extension}`)
        try {
          const { data } = await api.post<{ text: string }>(endpoint, form, { signal: controller.signal, timeout: 120_000 })
          if (version !== generation.current || !mounted.current) return
          if (!data.text?.trim()) throw new Error('empty-transcription')
          const current = latest.current
          const text = [current.value.trimEnd(), data.text.trim()].filter(Boolean).join('\n')
          if (text.length > current.maxLength) { setError(`A transcrição excede o limite de ${current.maxLength} caracteres. Grave uma mensagem mais curta.`); return }
          current.onChange(text)
        } catch (failure) {
          if (version === generation.current && mounted.current) setError(getErrorMessage(failure))
        } finally {
          if (version === generation.current && mounted.current) { abort.current = null; setPhase('idle') }
        }
      }
      const started = Date.now()
      capture.start(1000)
      setSeconds(0)
      setPhase('recording')
      media.getTracks().forEach(track => track.addEventListener('ended', () => {
        if (capture.state === 'recording' && version === generation.current) { cancel(); setError('O microfone foi interrompido. Grave novamente.') }
      }, { once: true }))
      timer.current = setInterval(() => {
        const elapsed = Math.floor((Date.now() - started) / 1000)
        setSeconds(Math.min(elapsed, MAX_SECONDS))
        if (elapsed >= MAX_SECONDS && capture.state === 'recording') capture.stop()
      }, 500)
    } catch (failure) {
      if (version !== generation.current || !mounted.current) return
      cancel()
      const name = (failure as { name?: string }).name
      setError(name === 'NotAllowedError' ? 'Permissão de microfone negada. Autorize o microfone nas configurações do navegador.'
        : name === 'NotFoundError' ? 'Nenhum microfone disponível.' : 'Não foi possível iniciar a gravação. Tente novamente.')
    }
  }

  const control = phase === 'idle' ? <Tooltip title="Gravar para transcrever"><span><IconButton aria-label="Gravar para transcrever" disabled={disabled || !active} onClick={() => void start()}><MicRoundedIcon /></IconButton></span></Tooltip>
    : <Stack direction="row" alignItems="center" flexWrap="wrap" gap={.25} role="status" aria-live="polite" sx={{ minWidth: 0 }}>
      {phase === 'recording' ? <Typography color="error.main" fontWeight={700}>● Gravando {`${Math.floor(seconds / 60)}`.padStart(2, '0')}:{`${seconds % 60}`.padStart(2, '0')}</Typography>
        : <><CircularProgress size={16} /><Typography>{phase === 'requesting' ? 'Aguardando microfone…' : 'Transcrevendo…'}</Typography></>}
      <Button size="small" color="inherit" onClick={cancel}>Cancelar</Button>
      {phase === 'recording' && <Button size="small" variant="outlined" startIcon={<StopRoundedIcon />} onClick={() => { if (recorder.current?.state === 'recording') recorder.current.stop() }}>Concluir</Button>}
    </Stack>
  return <>
    {inline ? <InputAdornment position="end" sx={{ alignSelf: 'flex-end', ml: .5 }}>{control}{error && <Tooltip title={error}><Typography component="span" role="alert" color="warning.main" aria-label={error} sx={{ px: .5 }}>!</Typography></Tooltip>}</InputAdornment> : <Box sx={{ minWidth: 0 }}>{control}{error && <Alert severity="warning" sx={{ mt: .5 }}>{error}</Alert>}</Box>}
  </>
}
