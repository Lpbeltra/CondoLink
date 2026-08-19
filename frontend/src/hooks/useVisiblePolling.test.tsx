import { act, render } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { useVisiblePolling } from './useVisiblePolling'

function Probe({ refresh }: { refresh: () => void | Promise<void> }) {
  useVisiblePolling(refresh, 1_000)
  return null
}

describe('useVisiblePolling', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    Object.defineProperty(document, 'visibilityState', { configurable: true, value: 'visible' })
  })

  afterEach(() => vi.useRealTimers())

  it('polls only while visible and refreshes when the tab returns', async () => {
    const refresh = vi.fn()
    render(<Probe refresh={refresh} />)

    await act(async () => vi.advanceTimersByTime(1_000))
    expect(refresh).toHaveBeenCalledTimes(1)

    Object.defineProperty(document, 'visibilityState', { configurable: true, value: 'hidden' })
    await act(async () => vi.advanceTimersByTime(1_000))
    expect(refresh).toHaveBeenCalledTimes(1)

    Object.defineProperty(document, 'visibilityState', { configurable: true, value: 'visible' })
    await act(async () => document.dispatchEvent(new Event('visibilitychange')))
    expect(refresh).toHaveBeenCalledTimes(2)
  })

  it('does not overlap polls and cleans up when unmounted', async () => {
    let finish!: () => void
    const refresh = vi.fn(() => new Promise<void>((resolve) => { finish = resolve }))
    const view = render(<Probe refresh={refresh} />)

    act(() => vi.advanceTimersByTime(3_000))
    expect(refresh).toHaveBeenCalledTimes(1)

    await act(async () => finish())
    act(() => vi.advanceTimersByTime(1_000))
    expect(refresh).toHaveBeenCalledTimes(2)

    view.unmount()
    await act(async () => finish())
    act(() => vi.advanceTimersByTime(2_000))
    expect(refresh).toHaveBeenCalledTimes(2)
  })
})
