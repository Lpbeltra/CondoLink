import { useEffect, useRef } from 'react'

export const portalPollingIntervalMs = 15_000

export function useVisiblePolling(refresh: () => void | Promise<void>,
  intervalMs = portalPollingIntervalMs) {
  const inFlight = useRef(false)
  useEffect(() => {
    const tick = () => {
      if (document.visibilityState !== 'visible' || inFlight.current) return
      inFlight.current = true
      Promise.resolve(refresh()).finally(() => { inFlight.current = false })
    }
    const visibilityChanged = () => {
      if (document.visibilityState === 'visible') tick()
    }
    const timer = window.setInterval(tick, intervalMs)
    document.addEventListener('visibilitychange', visibilityChanged)
    return () => {
      window.clearInterval(timer)
      document.removeEventListener('visibilitychange', visibilityChanged)
    }
  }, [intervalMs, refresh])
}
