import type { ManagementCompany } from './types'

export const managementCompanyDetailTabs = [
  { value: 'overview', label: 'Visão geral' },
  { value: 'employees', label: 'Funcionários' },
  { value: 'categories', label: 'Categorias' },
] as const

export function managementCompanyDetailsPath(id: string) {
  return `/overwatch/management-companies/${id}`
}

export function upsertManagementCompany(
  companies: ManagementCompany[],
  saved: ManagementCompany,
) {
  const withoutSaved = companies.filter((company) => company.id !== saved.id)
  return [...withoutSaved, saved].sort((left, right) =>
    left.name.localeCompare(right.name))
}
