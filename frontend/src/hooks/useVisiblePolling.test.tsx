import { act, render } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { useVisiblePolling } from './useVisiblePolling'

function Probe({ refresh }: { refresh: () => void }) {
  useVisiblePolling(refresh, 1_000)
  return null
}

describe('useVisiblePolling', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    Object.defineProperty(document, 'hidden', { configurable: true, value: false })
  })

  afterEach(() => vi.useRealTimers())

  it('polls only while visible and refreshes when the tab returns', () => {
    const refresh = vi.fn()
    render(<Probe refresh={refresh} />)

    act(() => vi.advanceTimersByTime(1_000))
    expect(refresh).toHaveBeenCalledTimes(1)

    Object.defineProperty(document, 'hidden', { configurable: true, value: true })
    act(() => vi.advanceTimersByTime(1_000))
    expect(refresh).toHaveBeenCalledTimes(1)

    Object.defineProperty(document, 'hidden', { configurable: true, value: false })
    act(() => document.dispatchEvent(new Event('visibilitychange')))
    expect(refresh).toHaveBeenCalledTimes(2)
  })
})
