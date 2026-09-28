import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { api } from '../services/api'
import { VoiceInput } from './VoiceInput'

class Recorder {
  static isTypeSupported = vi.fn((type: string) => Boolean(type))
  static instances: Recorder[] = []
  state = 'inactive'
  mimeType: string
  ondataavailable: ((event: { data: Blob }) => void) | null = null
  onstop: (() => void) | null = null
  onerror: (() => void) | null = null
  constructor(_stream: MediaStream, options: MediaRecorderOptions) { this.mimeType = options.mimeType!; Recorder.instances.push(this) }
  start() { this.state = 'recording' }
  stop() {
    this.state = 'inactive'
    this.ondataavailable?.({ data: new Blob(['recorded voice'], { type: this.mimeType }) })
    this.onstop?.()
  }
}
const track = { stop: vi.fn(), addEventListener: vi.fn() }
const media = { getTracks: () => [track] } as unknown as MediaStream
const getUserMedia = vi.fn()
function Composer({ endpoint = '/voice', active = true }: { endpoint?: string; active?: boolean }) {
  const [text, setText] = useState('Texto existente')
  const [busy, setBusy] = useState(false)
  return <><textarea aria-label="Mensagem" value={text} onChange={event => setText(event.target.value)} />
    <VoiceInput endpoint={endpoint} active={active} value={text} onChange={setText} maxLength={3000} onBusyChange={setBusy} />
    <button disabled={busy}>Enviar</button></>
}

describe('voice dictation lifecycle', () => {
  beforeEach(() => {
    Recorder.instances = []
    Recorder.isTypeSupported.mockReturnValue(true)
    getUserMedia.mockReset().mockResolvedValue(media)
    track.stop.mockReset()
    vi.stubGlobal('MediaRecorder', Recorder)
    Object.defineProperty(navigator, 'mediaDevices', { configurable: true, value: { getUserMedia } })
    vi.spyOn(api, 'post').mockResolvedValue({ data: { text: 'Texto ditado' } })
  })
  async function begin() { await userEvent.click(screen.getByRole('button', { name: 'Ditar mensagem' })); await screen.findByText(/Gravando 00:00/) }

  it('records, transcribes, appends and allows editing without sending', async () => {
    render(<Composer />)
    await begin()
    expect(getUserMedia).toHaveBeenCalledWith({ audio: true })
    expect(screen.getByRole('button', { name: 'Enviar' })).toBeDisabled()
    await userEvent.click(screen.getByRole('button', { name: 'Concluir' }))
    await waitFor(() => expect(screen.getByLabelText('Mensagem')).toHaveValue('Texto existente\nTexto ditado'))
    expect(api.post).toHaveBeenCalledTimes(1)
    const [path, form] = vi.mocked(api.post).mock.calls[0]
    expect(path).toBe('/voice')
    expect((form as FormData).get('audio')).toBeInstanceOf(File)
    expect(track.stop).toHaveBeenCalled()
    await userEvent.type(screen.getByLabelText('Mensagem'), ' revisado')
    expect(screen.getByLabelText('Mensagem')).toHaveValue('Texto existente\nTexto ditado revisado')
    expect(api.post).toHaveBeenCalledTimes(1)
    expect(screen.getByRole('button', { name: 'Enviar' })).toBeEnabled()
  })
  it('cancels without uploading or changing the existing text', async () => {
    render(<Composer />); await begin()
    await userEvent.click(screen.getByRole('button', { name: 'Cancelar' }))
    expect(screen.getByLabelText('Mensagem')).toHaveValue('Texto existente')
    expect(api.post).not.toHaveBeenCalled()
    expect(track.stop).toHaveBeenCalled()
  })
  it('explains a denied permission and permits retry', async () => {
    getUserMedia.mockRejectedValueOnce(new DOMException('denied', 'NotAllowedError'))
    render(<Composer />)
    await userEvent.click(screen.getByRole('button', { name: 'Ditar mensagem' }))
    expect(await screen.findByText(/Permissão de microfone negada/)).toBeVisible()
    await begin()
    expect(api.post).not.toHaveBeenCalled()
  })
  it('keeps text intact after a transcription failure', async () => {
    vi.mocked(api.post).mockRejectedValue(new Error('failed'))
    render(<Composer />); await begin()
    await userEvent.click(screen.getByRole('button', { name: 'Concluir' }))
    expect(await screen.findByRole('alert')).toBeVisible()
    expect(screen.getByLabelText('Mensagem')).toHaveValue('Texto existente')
  })
  it('cancels on background and unmount, stopping all microphone tracks', async () => {
    const page = render(<Composer />); await begin()
    Object.defineProperty(document, 'visibilityState', { configurable: true, value: 'hidden' })
    act(() => document.dispatchEvent(new Event('visibilitychange')))
    expect(track.stop).toHaveBeenCalled()
    expect(api.post).not.toHaveBeenCalled()
    Object.defineProperty(document, 'visibilityState', { configurable: true, value: 'visible' })
    await begin(); page.unmount()
    expect(track.stop).toHaveBeenCalledTimes(2)
  })
  it('discards permission results after cancellation', async () => {
    let resolve!: (stream: MediaStream) => void
    getUserMedia.mockReturnValue(new Promise<MediaStream>(done => { resolve = done }))
    render(<Composer />)
    await userEvent.click(screen.getByRole('button', { name: 'Ditar mensagem' }))
    await userEvent.click(screen.getByRole('button', { name: 'Cancelar' }))
    await act(async () => resolve(media))
    expect(track.stop).toHaveBeenCalled()
    expect(Recorder.instances).toHaveLength(0)
  })
  it('prevents simultaneous microphone captures', async () => {
    render(<><Composer /><Composer /></>)
    const microphones = screen.getAllByRole('button', { name: 'Ditar mensagem' })
    await userEvent.click(microphones[0])
    await screen.findByText(/Gravando/)
    await userEvent.click(microphones[1])
    expect(await screen.findByText(/Já existe uma gravação/)).toBeVisible()
    expect(getUserMedia).toHaveBeenCalledTimes(1)
  })
  it('uses MP4 when WebM is unsupported', async () => {
    Recorder.isTypeSupported.mockImplementation(type => type === 'audio/mp4')
    render(<Composer />); await begin()
    expect(Recorder.instances[0].mimeType).toBe('audio/mp4')
  })
  it('aborts processing when the composer closes and ignores late text', async () => {
    let resolve!: (value: { data: { text: string } }) => void
    vi.mocked(api.post).mockReturnValue(new Promise(done => { resolve = done }))
    const page = render(<Composer />); await begin()
    await userEvent.click(screen.getByRole('button', { name: 'Concluir' }))
    await screen.findByText('Transcrevendo…')
    const options = vi.mocked(api.post).mock.calls[0][2]
    page.rerender(<Composer active={false} />)
    expect(options?.signal?.aborted).toBe(true)
    await act(async () => resolve({ data: { text: 'Late text' } }))
    expect(screen.getByLabelText('Mensagem')).toHaveValue('Texto existente')
  })
  it('rejects an overlong transcription without truncating or replacing the draft', async () => {
    vi.mocked(api.post).mockResolvedValue({ data: { text: 'a'.repeat(3001) } })
    render(<Composer />); await begin()
    await userEvent.click(screen.getByRole('button', { name: 'Concluir' }))
    expect(await screen.findByText(/excede o limite de 3000/)).toBeVisible()
    expect(screen.getByLabelText('Mensagem')).toHaveValue('Texto existente')
  })
})
