import { beforeEach, describe, expect, it, vi } from 'vitest'
import {
  PWA_UPDATE_CHECK_DEBOUNCE_MS,
  PWA_UPDATE_CHECK_INTERVAL_MS,
  checkForPwaUpdate,
  getPwaUpdateState,
  resetPwaUpdateForTests,
  setPwaRegistration,
  startPwaUpdateChecks,
} from './pwaUpdate'

function registration(update = vi.fn().mockResolvedValue(undefined)) {
  return { update } as unknown as ServiceWorkerRegistration
}

function setOnline(value = true) {
  Object.defineProperty(navigator, 'onLine', { configurable: true, value })
}

function setVisibility(value: DocumentVisibilityState) {
  Object.defineProperty(document, 'visibilityState', { configurable: true, value })
}

describe('PWA update checks', () => {
  beforeEach(() => {
    resetPwaUpdateForTests()
    setOnline()
    setVisibility('visible')
  })

  it('stores the registration and checks at boot', async () => {
    const current = registration()
    setPwaRegistration(current)
    await checkForPwaUpdate()
    expect(getPwaUpdateState().registration).toBe(current)
    expect(current.update).toHaveBeenCalledOnce()
  })

  it('checks when connection returns', async () => {
    const current = registration()
    setPwaRegistration(current)
    startPwaUpdateChecks()
    window.dispatchEvent(new Event('online'))
    await vi.waitFor(() => expect(current.update).toHaveBeenCalledOnce())
  })

  it('checks only when the application becomes visible', async () => {
    const current = registration()
    setPwaRegistration(current)
    startPwaUpdateChecks()
    setVisibility('hidden')
    document.dispatchEvent(new Event('visibilitychange'))
    expect(current.update).not.toHaveBeenCalled()

    setVisibility('visible')
    document.dispatchEvent(new Event('visibilitychange'))
    await vi.waitFor(() => expect(current.update).toHaveBeenCalledOnce())
  })

  it('checks every 30 minutes while open', async () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-09-10T12:00:00Z'))
    const current = registration()
    setPwaRegistration(current)
    startPwaUpdateChecks()

    await vi.advanceTimersByTimeAsync(PWA_UPDATE_CHECK_INTERVAL_MS)
    expect(current.update).toHaveBeenCalledOnce()
  })

  it('deduplicates concurrent and immediately repeated checks', async () => {
    let finish!: () => void
    const update = vi.fn(() => new Promise<void>(resolve => { finish = resolve }))
    const current = registration(update)
    setPwaRegistration(current)
    startPwaUpdateChecks()

    window.dispatchEvent(new Event('online'))
    document.dispatchEvent(new Event('visibilitychange'))
    await vi.waitFor(() => expect(update).toHaveBeenCalledOnce())
    finish()
    await Promise.resolve()
    await Promise.resolve()
    document.dispatchEvent(new Event('visibilitychange'))
    expect(update).toHaveBeenCalledOnce()
  })

  it('does not check without a registration or while offline', async () => {
    await checkForPwaUpdate()
    const current = registration()
    setPwaRegistration(current)
    setOnline(false)
    await checkForPwaUpdate()
    expect(current.update).not.toHaveBeenCalled()
  })

  it('contains check failures and permits a later retry', async () => {
    vi.spyOn(console, 'error').mockImplementation(() => undefined)
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-09-10T12:00:00Z'))
    const update = vi.fn()
      .mockRejectedValueOnce(new Error('check failed'))
      .mockResolvedValueOnce(undefined)
    setPwaRegistration(registration(update))

    await checkForPwaUpdate()
    expect(getPwaUpdateState().error).toBe('Não foi possível verificar atualizações agora.')

    vi.advanceTimersByTime(PWA_UPDATE_CHECK_DEBOUNCE_MS)
    await checkForPwaUpdate()
    expect(update).toHaveBeenCalledTimes(2)
    expect(getPwaUpdateState().error).toBeNull()
  })
})
