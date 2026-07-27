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
  createManagementCompany,
  createManagementCompanyEmployee,
  listManagementCompanyEmployees,
  removeManagementCompanyEmployee,
  updateManagementCompanyEmployeeStatus,
} from './api'

beforeEach(() => {
  vi.clearAllMocks()
})

describe('management company API', () => {
  it('creates a management company with the exact payload', async () => {
    const input = {
      name: 'Admin',
      cnpj: '11222333000181', address: 'Rua A', city: 'São Paulo', state: 'SP',
      email: 'admin@example.com',
      phoneNumber: null,
    }
    const created = { id: 'company-id', ...input }
    http.post.mockResolvedValue({ data: created })

    await expect(createManagementCompany(input)).resolves.toBe(created)
    expect(http.post).toHaveBeenCalledWith(
      '/overwatch/management-companies',
      input,
    )
  })

  it('uses the real employee list and creation routes', async () => {
    http.get.mockResolvedValue({ data: [] })
    http.post.mockResolvedValue({ data: { id: 'employee-id' } })

    await listManagementCompanyEmployees('company-id')
    await createManagementCompanyEmployee('company-id', {
      fullName: 'Employee',
      email: 'employee@example.com',
      contact: null,
      jobTitle: 'Atendimento',
    })

    expect(http.get).toHaveBeenCalledWith(
      '/overwatch/management-companies/company-id/employees',
    )
    expect(http.post).toHaveBeenCalledWith(
      '/overwatch/management-companies/company-id/employees',
      {
        fullName: 'Employee', email: 'employee@example.com',
        contact: null, jobTitle: 'Atendimento',
      },
    )
  })

  it('uses the real status route and handles DELETE 204 without parsing data', async () => {
    http.patch.mockResolvedValue({ data: { id: 'employee-id', isActive: false } })
    http.delete.mockResolvedValue({ status: 204, data: '' })

    await updateManagementCompanyEmployeeStatus('employee-id', false)
    await expect(removeManagementCompanyEmployee('employee-id'))
      .resolves.toBeUndefined()

    expect(http.patch).toHaveBeenCalledWith(
      '/employees/employee-id/status',
      { isActive: false },
    )
    expect(http.delete).toHaveBeenCalledWith('/employees/employee-id')
  })
})
