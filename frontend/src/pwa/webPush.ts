import { api } from '../services/api'

export interface WebPushConfig {
  enabled: boolean
  vapidPublicKey: string | null
}

export function supportsWebPush() {
  return typeof window !== 'undefined'
    && 'Notification' in window
    && 'serviceWorker' in navigator
    && 'PushManager' in window
}

export async function getWebPushConfig() {
  return (await api.get<WebPushConfig>('/users/me/push/config')).data
}

export async function currentPushSubscription() {
  if (!supportsWebPush()) return null
  const registration = await navigator.serviceWorker.ready
  return registration.pushManager.getSubscription()
}

export async function activateWebPush(vapidPublicKey: string) {
  const registration = await navigator.serviceWorker.ready
  const existing = await registration.pushManager.getSubscription()
  const subscription = existing ?? await registration.pushManager.subscribe({
    userVisibleOnly: true,
    applicationServerKey: urlBase64ToUint8Array(vapidPublicKey),
  })
  const json = subscription.toJSON()
  if (!json.endpoint || !json.keys?.p256dh || !json.keys.auth) {
    if (!existing) await subscription.unsubscribe().catch(() => false)
    throw new Error('Invalid PushSubscription')
  }
  try {
    await api.post('/users/me/push/subscriptions', {
      endpoint: json.endpoint,
      keys: { p256dh: json.keys.p256dh, auth: json.keys.auth },
    })
    return subscription
  } catch (error) {
    if (!existing) await subscription.unsubscribe().catch(() => false)
    throw error
  }
}

export async function deactivateWebPush(subscription: PushSubscription) {
  await api.delete('/users/me/push/subscriptions', {
    data: { endpoint: subscription.endpoint },
    _refreshRetried: true,
  } as never)
  await subscription.unsubscribe()
}

export async function deactivateCurrentPushSubscription() {
  if (!supportsWebPush()) return
  const registration = await navigator.serviceWorker.getRegistration()
  const subscription = await registration?.pushManager.getSubscription()
  if (!subscription) return
  try {
    await api.delete('/users/me/push/subscriptions', {
      data: { endpoint: subscription.endpoint },
      _refreshRetried: true,
      timeout: 3_000,
    } as never)
  } finally {
    await subscription.unsubscribe().catch(() => false)
  }
}

export function urlBase64ToUint8Array(value: string) {
  const padding = '='.repeat((4 - value.length % 4) % 4)
  const base64 = (value + padding).replace(/-/g, '+').replace(/_/g, '/')
  const raw = window.atob(base64)
  return Uint8Array.from(raw, character => character.charCodeAt(0))
}
