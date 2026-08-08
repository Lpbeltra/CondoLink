import { useEffect } from 'react'

export const portalPollingIntervalMs = 15_000

export function useVisiblePolling(refresh: () => void,
  intervalMs = portalPollingIntervalMs) {
  useEffect(() => {
    const tick = () => { if (!document.hidden) refresh() }
    const visibilityChanged = () => { if (!document.hidden) refresh() }
    const timer = window.setInterval(tick, intervalMs)
    document.addEventListener('visibilitychange', visibilityChanged)
    return () => {
      window.clearInterval(timer)
      document.removeEventListener('visibilitychange', visibilityChanged)
    }
  }, [intervalMs, refresh])
}
