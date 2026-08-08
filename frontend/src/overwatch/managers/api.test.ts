import { beforeEach, describe, expect, it, vi } from 'vitest'

const http = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  patch: vi.fn(),
  delete: vi.fn(),
}))
vi.mock('../../services/api', () => ({ api: http }))

import {
  createManager,
  getCondominiumManager,
  getManager,
  linkManager,
  listAvailableCondominiums,
  listCondominiumManagers,
  listManagerCondominiums,
  listManagerCandidates,
  listManagers,
  removeManagerLink,
  replaceCondominiumManager,
  updateManagerStatus,
} from './api'

beforeEach(() => vi.clearAllMocks())

describe('Overwatch manager API', () => {
  it('uses the real list, details and relationships endpoints', async () => {
    http.get.mockResolvedValue({ data: [] })
    await listManagers()
    await listManagerCandidates()
    await getManager('manager-id')
    await listManagerCondominiums('manager-id')
    await listCondominiumManagers('condominium-id')
    await getCondominiumManager('condominium-id')
    await listAvailableCondominiums()

    expect(http.get).toHaveBeenNthCalledWith(1, '/overwatch/managers')
    expect(http.get).toHaveBeenNthCalledWith(
      2, '/overwatch/managers?eligibleForAssignment=true',
    )
    expect(http.get).toHaveBeenNthCalledWith(3, '/overwatch/managers/manager-id')
    expect(http.get).toHaveBeenNthCalledWith(
      4, '/overwatch/managers/manager-id/condominiums',
    )
    expect(http.get).toHaveBeenNthCalledWith(
      5, '/overwatch/condominiums/condominium-id/managers',
    )
    expect(http.get).toHaveBeenNthCalledWith(
      6, '/overwatch/condominiums/condominium-id/manager',
    )
    expect(http.get).toHaveBeenNthCalledWith(7, '/overwatch/condominiums')
  })

  it('sends the complete profile on creation', async () => {
    const input = {
      fullName: 'Manager', email: 'manager@example.com', phoneNumber: null,
      cpf: null, cnpj: null, address: null, city: null, state: null,
    }
    http.post.mockResolvedValue({ data: { id: 'manager-id' } })
    await createManager(input)
    expect(http.post).toHaveBeenCalledWith('/overwatch/managers', input)
  })

  it('uses the status and link contracts', async () => {
    http.patch.mockResolvedValue({ data: {} })
    http.post.mockResolvedValue({ data: {} })
    http.put.mockResolvedValue({ data: {} })
    await updateManagerStatus('manager-id', false)
    await linkManager('manager-id', 'condominium-id')
    await replaceCondominiumManager('condominium-id', 'next-manager-id')

    expect(http.patch).toHaveBeenCalledWith(
      '/overwatch/managers/manager-id/status', { isActive: false },
    )
    expect(http.post).toHaveBeenCalledWith(
      '/overwatch/management-memberships',
      { managerId: 'manager-id', condominiumId: 'condominium-id' },
    )
    expect(http.put).toHaveBeenCalledWith(
      '/overwatch/condominiums/condominium-id/manager',
      { managerId: 'next-manager-id' },
    )
  })

  it('accepts 204 when removing a relationship', async () => {
    http.delete.mockResolvedValue({ status: 204 })
    await removeManagerLink('manager-id', 'condominium-id')
    expect(http.delete).toHaveBeenCalledWith(
      '/overwatch/managers/manager-id/condominiums/condominium-id',
    )
  })
})
