import { act, renderHook, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useCondominiumModules } from './useCondominiumModules'

const state = vi.hoisted(() => ({ get: vi.fn(), activeCondominiumId: 'A' as string | null }))
const { get } = state
vi.mock('../management/ManagementContext', () => ({ useManagementContext: () => ({ activeCondominiumId: state.activeCondominiumId }) }))
vi.mock('../services/api', () => ({ api: { get: state.get } }))

describe('useCondominiumModules', () => {
  beforeEach(() => { state.activeCondominiumId = 'A'; get.mockReset() })

  it('loads entitlement once, exposes enabled state, refreshes and clears prior error', async () => {
    get.mockResolvedValueOnce({ data: { modules: [{ module: 'Assistant', enabled: true }] } })
      .mockRejectedValueOnce(new Error('offline'))
      .mockResolvedValueOnce({ data: { modules: [{ module: 'Assistant', enabled: false }] } })
    const { result } = renderHook(() => useCondominiumModules())
    expect(result.current.loading).toBe(true)
    await waitFor(() => expect(result.current.isModuleEnabled('Assistant')).toBe(true))
    expect(get).toHaveBeenCalledTimes(1)
    await act(async () => { await result.current.refresh() })
    expect(result.current.error).toContain('Não foi possível')
    await act(async () => { await result.current.refresh() })
    expect(result.current.error).toBe('')
    expect(result.current.isModuleEnabled('Assistant')).toBe(false)
  })

  it('does not retain entitlement across context change or consolidated view', async () => {
    get.mockImplementation(() => Promise.resolve({ data: { modules: [{ module: 'Assistant', enabled: state.activeCondominiumId === 'A' }] } }))
    const { result, rerender } = renderHook(() => useCondominiumModules())
    await waitFor(() => expect(result.current.isModuleEnabled('Assistant')).toBe(true))
    state.activeCondominiumId = 'B'; rerender()
    await waitFor(() => expect(result.current.isModuleEnabled('Assistant')).toBe(false))
    state.activeCondominiumId = null; rerender()
    await waitFor(() => expect(result.current.modules).toEqual([]))
    expect(result.current.isModuleEnabled('Assistant')).toBe(false)
    expect(get).toHaveBeenCalledTimes(2)
  })
})
