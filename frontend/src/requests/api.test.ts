import { describe, expect, it, vi } from 'vitest'

const http = vi.hoisted(() => ({ get: vi.fn() }))
vi.mock('../services/api', () => ({ api: http }))

import { listManagementRequests } from './api'

describe('management requests API', () => {
  it('omits a condominium in consolidated mode and sends it in specific mode', async () => {
    http.get.mockResolvedValue({ data: {} })

    await listManagementRequests({ status: 'Open' })
    await listManagementRequests({
      status: 'Open',
      condominiumId: 'condominium-id',
    })

    expect(http.get).toHaveBeenNthCalledWith(
      1,
      '/management/requests',
      { params: { status: 'Open' } },
    )
    expect(http.get).toHaveBeenNthCalledWith(
      2,
      '/management/requests',
      {
        params: {
          status: 'Open',
          condominiumId: 'condominium-id',
        },
      },
    )
  })
})
