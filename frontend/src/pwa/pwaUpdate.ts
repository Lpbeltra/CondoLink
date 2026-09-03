export type PwaUpdateHandler = (reloadPage?: boolean) => Promise<void>

let updateServiceWorker: PwaUpdateHandler | null = null
const updateEvent = 'comvy:pwa-update-available'

export function setPwaUpdateHandler(handler: PwaUpdateHandler) { updateServiceWorker = handler }
export function notifyPwaUpdateAvailable() { window.dispatchEvent(new Event(updateEvent)) }
export function subscribeToPwaUpdate(handler: () => void) {
  window.addEventListener(updateEvent, handler)
  return () => window.removeEventListener(updateEvent, handler)
}
export async function applyPwaUpdate() { await updateServiceWorker?.(true) }
