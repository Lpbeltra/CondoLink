import { act, render, screen, waitFor } from '@testing-library/react'
import { useCallback, useState } from 'react'
import { describe, expect, it, vi } from 'vitest'
import { useGuardedLoad } from './useGuardedLoad'

const toMessage = (error: unknown) =>
  error instanceof Error ? error.message : 'erro'

/** Renders the hook against a fetcher keyed by `id`, so we can flip the key. */
function Probe({ fetchById }: { fetchById: (id: string) => Promise<string> }) {
  const [id, setId] = useState('a')
  const fetcher = useCallback(() => fetchById(id), [fetchById, id])
  const { data, isLoading, error } = useGuardedLoad(fetcher, toMessage)

  return (
    <div>
      <output data-testid="id">{id}</output>
      <output data-testid="data">{data ?? '-'}</output>
      <output data-testid="loading">{isLoading ? 'yes' : 'no'}</output>
      <output data-testid="error">{error || '-'}</output>
      <button onClick={() => setId('b')}>go-b</button>
    </div>
  )
}

describe('useGuardedLoad', () => {
  it('exposes the resolved value and clears loading', async () => {
    render(<Probe fetchById={async (id) => `value-${id}`} />)
    await waitFor(() => expect(screen.getByTestId('data')).toHaveTextContent('value-a'))
    expect(screen.getByTestId('loading')).toHaveTextContent('no')
    expect(screen.getByTestId('error')).toHaveTextContent('-')
  })

  it('discards a slow first response when a newer request already resolved', async () => {
    const resolvers: Record<string, (value: string) => void> = {}
    const fetchById = vi.fn(
      (id: string) =>
        new Promise<string>((resolve) => {
          resolvers[id] = resolve
        }),
    )

    render(<Probe fetchById={fetchById} />)
    await waitFor(() => expect(fetchById).toHaveBeenCalledWith('a'))

    // Navigate to "b" while "a" is still in flight.
    act(() => { screen.getByText('go-b').click() })
    await waitFor(() => expect(fetchById).toHaveBeenCalledWith('b'))

    // "b" wins, then the stale "a" finally lands.
    await act(async () => { resolvers.b('value-b') })
    await waitFor(() => expect(screen.getByTestId('data')).toHaveTextContent('value-b'))

    await act(async () => { resolvers.a('value-a') })

    // The stale response must NOT overwrite the current one.
    expect(screen.getByTestId('data')).toHaveTextContent('value-b')
    expect(screen.getByTestId('id')).toHaveTextContent('b')
  })

  it('ignores a stale rejection so it cannot clobber a good state', async () => {
    const resolvers: Record<string, (value: string) => void> = {}
    const rejecters: Record<string, (error: Error) => void> = {}
    const fetchById = vi.fn(
      (id: string) =>
        new Promise<string>((resolve, reject) => {
          resolvers[id] = resolve
          rejecters[id] = reject
        }),
    )

    render(<Probe fetchById={fetchById} />)
    await waitFor(() => expect(fetchById).toHaveBeenCalledWith('a'))

    act(() => { screen.getByText('go-b').click() })
    await waitFor(() => expect(fetchById).toHaveBeenCalledWith('b'))

    await act(async () => { resolvers.b('value-b') })
    await act(async () => { rejecters.a(new Error('falha antiga')) })

    expect(screen.getByTestId('data')).toHaveTextContent('value-b')
    expect(screen.getByTestId('error')).toHaveTextContent('-')
  })

  it('surfaces an error from the current request', async () => {
    render(<Probe fetchById={async () => { throw new Error('indisponível') }} />)
    await waitFor(() => expect(screen.getByTestId('error')).toHaveTextContent('indisponível'))
    expect(screen.getByTestId('loading')).toHaveTextContent('no')
  })

  it('refetches when the fetcher identity changes', async () => {
    const fetchById = vi.fn(async (id: string) => `value-${id}`)
    render(<Probe fetchById={fetchById} />)
    await waitFor(() => expect(screen.getByTestId('data')).toHaveTextContent('value-a'))

    act(() => { screen.getByText('go-b').click() })
    await waitFor(() => expect(screen.getByTestId('data')).toHaveTextContent('value-b'))
    expect(fetchById).toHaveBeenCalledTimes(2)
  })

  it('does not set state after unmount', async () => {
    let resolve: ((value: string) => void) | undefined
    const view = render(
      <Probe fetchById={() => new Promise<string>((r) => { resolve = r })} />,
    )
    await waitFor(() => expect(resolve).toBeDefined())

    view.unmount()
    // Would trigger a React "update on unmounted component" warning if unguarded.
    await act(async () => { resolve?.('late') })
  })
})
