import { beforeEach, describe, expect, it, vi } from 'vitest'

const http = vi.hoisted(() => ({
  get: vi.fn(),
  put: vi.fn(),
}))
vi.mock('../services/api', () => ({ api: http }))

import { getManagementContext, setManagementContext } from './api'

beforeEach(() => vi.clearAllMocks())

describe('management context API', () => {
  it('loads the reconciled context and selects specific or consolidated scope', async () => {
    http.get.mockResolvedValue({ data: {} })
    http.put.mockResolvedValue({ data: {} })

    await getManagementContext()
    await setManagementContext('condominium-id')
    await setManagementContext(null)

    expect(http.get).toHaveBeenCalledWith('/management/context')
    expect(http.put).toHaveBeenNthCalledWith(
      1,
      '/management/context',
      { condominiumId: 'condominium-id' },
    )
    expect(http.put).toHaveBeenNthCalledWith(
      2,
      '/management/context',
      { condominiumId: null },
    )
  })
})
