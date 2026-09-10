import { api } from '../services/api'
import { describe, expect, it, vi } from 'vitest'
import { deactivateCurrentPushSubscription, supportsWebPush, urlBase64ToUint8Array } from './webPush'

describe('webPush helpers', () => {
  it('uses feature detection', () => {
    expect(supportsWebPush()).toBe(false)
  })

  it('decodes a VAPID base64url key', () => {
    expect(Array.from(urlBase64ToUint8Array('AQID'))).toEqual([1, 2, 3])
  })

  it('deactivates backend association before unsubscribing on logout', async () => {
    const unsubscribe = vi.fn().mockResolvedValue(true)
    const subscription = { endpoint: 'https://push.test/device', unsubscribe }
    Object.defineProperty(window, 'Notification', { configurable: true, value: { permission: 'granted' } })
    Object.defineProperty(window, 'PushManager', { configurable: true, value: class {} })
    Object.defineProperty(navigator, 'serviceWorker', { configurable: true, value: {
      getRegistration: vi.fn().mockResolvedValue({
        pushManager: { getSubscription: vi.fn().mockResolvedValue(subscription) },
      }),
    } })
    vi.spyOn(api, 'delete').mockResolvedValue({} as never)
    await deactivateCurrentPushSubscription()
    expect(api.delete).toHaveBeenCalledWith('/users/me/push/subscriptions', expect.objectContaining({
      data: { endpoint: subscription.endpoint },
    }))
    expect(unsubscribe).toHaveBeenCalled()
  })
})
