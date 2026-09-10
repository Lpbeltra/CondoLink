import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, expect, it, vi } from 'vitest'

type Handler = (event: Record<string, unknown>) => void

function loadWorker(existingWindows: Array<Record<string, unknown>> = []) {
  const handlers: Record<string, Handler> = {}
  const showNotification = vi.fn().mockResolvedValue(undefined)
  const openWindow = vi.fn().mockResolvedValue(undefined)
  const scope = {
    location: { origin: 'https://app.comvy.test' },
    registration: { showNotification },
    clients: {
      matchAll: vi.fn().mockResolvedValue(existingWindows),
      openWindow,
    },
    addEventListener: (name: string, handler: Handler) => { handlers[name] = handler },
  }
  const source = readFileSync(resolve(process.cwd(), 'public/push-notifications.js'), 'utf8')
  new Function('self', 'URL', source)(scope, URL)
  return { handlers, showNotification, openWindow, scope }
}

function fire(handler: Handler, event: Record<string, unknown>) {
  let work: Promise<unknown> = Promise.resolve()
  handler({ ...event, waitUntil: (promise: Promise<unknown>) => { work = promise } })
  return work
}

describe('push service worker', () => {
  it('shows a safe valid payload', async () => {
    const worker = loadWorker()
    await fire(worker.handlers.push, { data: { json: () => ({
      title: 'Comvy', body: 'Nova resposta.', url: '/requests/123',
    }) } })
    expect(worker.showNotification).toHaveBeenCalledWith('Comvy', expect.objectContaining({
      body: 'Nova resposta.', data: { url: '/requests/123' },
    }))
  })

  it('falls back safely for invalid payload and rejects external URLs', async () => {
    const worker = loadWorker()
    await fire(worker.handlers.push, { data: { json: () => { throw new Error('bad') } } })
    expect(worker.showNotification).toHaveBeenLastCalledWith('Comvy', expect.objectContaining({ data: { url: '/' } }))
    await fire(worker.handlers.push, { data: { json: () => ({ url: '//evil.test/path' }) } })
    expect(worker.showNotification).toHaveBeenLastCalledWith('Comvy', expect.objectContaining({ data: { url: '/' } }))
  })

  it('navigates and focuses an existing Comvy window', async () => {
    const navigate = vi.fn().mockResolvedValue(undefined)
    const focus = vi.fn().mockResolvedValue(undefined)
    const worker = loadWorker([{ url: 'https://app.comvy.test/', navigate, focus }])
    const close = vi.fn()
    await fire(worker.handlers.notificationclick, {
      notification: { close, data: { url: '/requests/42' } },
    })
    expect(close).toHaveBeenCalled()
    expect(navigate).toHaveBeenCalledWith('https://app.comvy.test/requests/42')
    expect(focus).toHaveBeenCalled()
  })

  it('opens a window when none exists and never opens an external URL', async () => {
    const worker = loadWorker()
    await fire(worker.handlers.notificationclick, {
      notification: { close: vi.fn(), data: { url: 'https://evil.test/' } },
    })
    expect(worker.openWindow).toHaveBeenCalledWith('https://app.comvy.test/')
  })
})
