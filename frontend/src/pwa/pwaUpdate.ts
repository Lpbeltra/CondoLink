export type PwaUpdateHandler = (reloadPage?: boolean) => Promise<void>

export interface PwaUpdateState {
  updateAvailable: boolean
  registration: ServiceWorkerRegistration | null
  updating: boolean
  error: string | null
}

export const PWA_UPDATE_CHECK_INTERVAL_MS = 30 * 60 * 1000
export const PWA_UPDATE_CHECK_DEBOUNCE_MS = 10_000

const initialState: PwaUpdateState = {
  updateAvailable: false,
  registration: null,
  updating: false,
  error: null,
}

let state = initialState
let updateServiceWorker: PwaUpdateHandler | null = null
let updateCheck: Promise<void> | null = null
let lastUpdateCheckAt = Number.NEGATIVE_INFINITY
let stopUpdateChecks: (() => void) | null = null
const listeners = new Set<() => void>()

function publish(patch: Partial<PwaUpdateState>) {
  state = { ...state, ...patch }
  listeners.forEach(listener => listener())
}

function reportTechnicalError(message: string) {
  console.error(`[PWA] ${message}`)
}

export function getPwaUpdateState() { return state }

export function subscribeToPwaUpdate(listener: () => void) {
  listeners.add(listener)
  return () => listeners.delete(listener)
}

export function setPwaUpdateHandler(handler: PwaUpdateHandler) {
  updateServiceWorker = handler
}

export function setPwaRegistration(registration: ServiceWorkerRegistration) {
  publish({ registration })
}

export function notifyPwaUpdateAvailable() {
  publish({ updateAvailable: true, error: null })
}

export function reportPwaRegistrationError() {
  publish({ error: 'Não foi possível verificar atualizações agora.' })
  reportTechnicalError('Service worker registration failed.')
}

export function checkForPwaUpdate() {
  const registration = state.registration
  if (!registration || !navigator.onLine) return Promise.resolve()
  if (updateCheck) return updateCheck

  const now = Date.now()
  if (now - lastUpdateCheckAt < PWA_UPDATE_CHECK_DEBOUNCE_MS) return Promise.resolve()
  lastUpdateCheckAt = now

  updateCheck = Promise.resolve()
    .then(() => registration.update())
    .then(() => { publish({ error: null }) })
    .catch(() => {
      publish({ error: 'Não foi possível verificar atualizações agora.' })
      reportTechnicalError('Service worker update check failed.')
    })
    .finally(() => { updateCheck = null })
  return updateCheck
}

export function startPwaUpdateChecks() {
  if (stopUpdateChecks) return stopUpdateChecks

  const check = () => { void checkForPwaUpdate() }
  const checkWhenVisible = () => {
    if (document.visibilityState === 'visible') check()
  }
  window.addEventListener('online', check)
  document.addEventListener('visibilitychange', checkWhenVisible)
  const interval = window.setInterval(check, PWA_UPDATE_CHECK_INTERVAL_MS)

  stopUpdateChecks = () => {
    window.removeEventListener('online', check)
    document.removeEventListener('visibilitychange', checkWhenVisible)
    window.clearInterval(interval)
    stopUpdateChecks = null
  }
  return stopUpdateChecks
}

export async function applyPwaUpdate() {
  if (state.updating) return
  if (!updateServiceWorker) {
    publish({ error: 'Não foi possível atualizar o Comvy. Tente novamente.' })
    reportTechnicalError('Service worker update handler is unavailable.')
    return
  }

  publish({ updating: true, error: null })
  try {
    await updateServiceWorker(true)
  } catch {
    publish({ updating: false, error: 'Não foi possível atualizar o Comvy. Tente novamente.' })
    reportTechnicalError('Service worker activation failed.')
  }
}

export function resetPwaUpdateForTests() {
  stopUpdateChecks?.()
  state = initialState
  updateServiceWorker = null
  updateCheck = null
  lastUpdateCheckAt = Number.NEGATIVE_INFINITY
  listeners.clear()
}
