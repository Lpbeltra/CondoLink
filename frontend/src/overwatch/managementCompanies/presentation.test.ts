import { describe, expect, it } from 'vitest'
import {
  managementCompanyDetailsPath,
  managementCompanyDetailTabs,
  upsertManagementCompany,
} from './presentation'
import type { ManagementCompany } from './types'

function company(id: string, name: string): ManagementCompany {
  return {
    id,
    name,
    legalName: null,
    document: null,
    email: null,
    phoneNumber: null,
    isActive: true,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    condominiumCount: 0,
    employeeCount: 0,
  }
}

describe('management company presentation', () => {
  it('adds a successful creation to the ordered list', () => {
    expect(upsertManagementCompany(
      [company('z', 'Zulu')],
      company('a', 'Alpha'),
    ).map((item) => item.name)).toEqual(['Alpha', 'Zulu'])
  })

  it('provides the visible details action destination and tabs', () => {
    expect(managementCompanyDetailsPath('company-id'))
      .toBe('/overwatch/management-companies/company-id')
    expect(managementCompanyDetailTabs.map((item) => item.label))
      .toEqual(['Visão geral', 'Funcionários', 'Categorias'])
  })
})
