import { beforeEach, describe, expect, it, vi } from 'vitest'

const http = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  patch: vi.fn(),
}))

vi.mock('../../services/api', () => ({ api: http }))

import {
  createOverwatchCondominium,
  getOverwatchCondominium,
  listManagementCompanyOptions,
  listOverwatchCondominiums,
  setCondominiumManagementCompany,
  updateOverwatchCondominium,
  updateOverwatchCondominiumStatus,
} from './api'

beforeEach(() => vi.clearAllMocks())

describe('Overwatch condominium API', () => {
  it('uses the real list and details endpoints', async () => {
    http.get.mockResolvedValue({ data: [] })

    await listOverwatchCondominiums()
    await getOverwatchCondominium('condominium-id')

    expect(http.get).toHaveBeenNthCalledWith(1, '/overwatch/condominiums')
    expect(http.get).toHaveBeenNthCalledWith(
      2,
      '/overwatch/condominiums/condominium-id',
    )
  })

  it('sends the exact creation payload with empty optional fields normalized', async () => {
    const input = {
      name: 'Condomínio', email: null, cnpj: '11222333000181',
      address: 'Rua A', city: 'São Paulo', state: 'SP', hasDoorman: false,
      isRemoteDoorman: false, doormanContact: null,
    }
    http.post.mockResolvedValue({ data: { id: 'condominium-id' } })

    await createOverwatchCondominium(input)

    expect(http.post).toHaveBeenCalledWith('/overwatch/condominiums', input)
  })

  it('uses the real update and status contracts', async () => {
    const input = {
      name: 'Condomínio atualizado',
      email: 'contact@example.com',
      cnpj: '11222333000181', address: 'Rua A', city: 'São Paulo', state: 'SP',
      hasDoorman: false, isRemoteDoorman: false, doormanContact: null,
    }
    http.put.mockResolvedValue({ data: {} })
    http.patch.mockResolvedValue({ data: {} })

    await updateOverwatchCondominium('condominium-id', input)
    await updateOverwatchCondominiumStatus('condominium-id', false)

    expect(http.put).toHaveBeenCalledWith(
      '/overwatch/condominiums/condominium-id',
      input,
    )
    expect(http.patch).toHaveBeenCalledWith(
      '/overwatch/condominiums/condominium-id/status',
      { isActive: false },
    )
  })

  it('sends the management company id and null when unlinking', async () => {
    http.put.mockResolvedValue({ data: {} })

    await setCondominiumManagementCompany('condominium-id', 'company-id')
    await setCondominiumManagementCompany('condominium-id', null)

    expect(http.put).toHaveBeenNthCalledWith(
      1,
      '/overwatch/condominiums/condominium-id/management-company',
      { managementCompanyId: 'company-id' },
    )
    expect(http.put).toHaveBeenNthCalledWith(
      2,
      '/overwatch/condominiums/condominium-id/management-company',
      { managementCompanyId: null },
    )
  })

  it('loads management company options from the existing endpoint', async () => {
    http.get.mockResolvedValue({ data: [] })

    await listManagementCompanyOptions()

    expect(http.get).toHaveBeenCalledWith('/overwatch/management-companies')
  })
})
