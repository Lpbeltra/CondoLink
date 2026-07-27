import { useCallback, useEffect, useRef, useState } from 'react'

export interface GuardedLoadState<T> {
  data: T | null
  isLoading: boolean
  error: string
  /** Re-runs the fetch, keeping the stale-response guard in place. */
  reload: () => Promise<void>
  setData: (value: T | null) => void
  setError: (value: string) => void
}

/**
 * Runs an async fetch and discards responses that arrive after a newer one was
 * started — the classic problem when a route param changes while a request is
 * in flight and React Router keeps the component mounted.
 *
 * Without this, navigating quickly between two detail pages can leave entity A
 * in state while the URL points at B, so a subsequent save writes to the wrong
 * record.
 *
 * `fetcher` must be memoised by the caller (useCallback) — it is the effect's
 * dependency.
 */
export function useGuardedLoad<T>(
  fetcher: () => Promise<T>,
  toErrorMessage: (error: unknown) => string,
): GuardedLoadState<T> {
  const [data, setData] = useState<T | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState('')
  const version = useRef(0)

  const reload = useCallback(async () => {
    const current = ++version.current
    setIsLoading(true)
    setError('')
    try {
      const result = await fetcher()
      if (current === version.current) setData(result)
    } catch (requestError) {
      if (current === version.current) setError(toErrorMessage(requestError))
    } finally {
      if (current === version.current) setIsLoading(false)
    }
  }, [fetcher, toErrorMessage])

  useEffect(() => { void reload() }, [reload])

  // Invalidate any in-flight response when the component goes away.
  useEffect(() => () => { version.current += 1 }, [])

  return { data, isLoading, error, reload, setData, setError }
}
