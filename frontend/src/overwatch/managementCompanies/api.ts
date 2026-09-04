import { api } from '../../services/api'
import type {
  CreatedManagementCompanyEmployee,
  EmployeeInput,
  ManagementCompany,
  ManagementCompanyEmployee,
  ManagementCompanyInput,
  ManagementCompanyCategory,
} from './types'

export async function listManagementCompanies() {
  return (await api.get<ManagementCompany[]>('/overwatch/management-companies')).data
}

export async function getManagementCompany(id: string) {
  return (await api.get<ManagementCompany>(`/overwatch/management-companies/${id}`)).data
}

export async function createManagementCompany(input: ManagementCompanyInput) {
  return (await api.post<ManagementCompany>('/overwatch/management-companies', input)).data
}

export async function updateManagementCompany(id: string, input: ManagementCompanyInput) {
  return (await api.put<ManagementCompany>(`/overwatch/management-companies/${id}`, input)).data
}

export async function updateManagementCompanyStatus(id: string, isActive: boolean) {
  return (await api.patch<{ id: string; name: string; isActive: boolean }>(
    `/overwatch/management-companies/${id}/status`,
    { isActive },
  )).data
}

export async function listManagementCompanyEmployees(managementCompanyId: string) {
  return (await api.get<ManagementCompanyEmployee[]>(
    `/overwatch/management-companies/${managementCompanyId}/employees`,
  )).data
}

export async function createManagementCompanyEmployee(
  managementCompanyId: string,
  input: EmployeeInput,
) {
  return (await api.post<CreatedManagementCompanyEmployee>(
    `/overwatch/management-companies/${managementCompanyId}/employees`,
    input,
  )).data
}

export async function updateManagementCompanyEmployeeStatus(
  employeeId: string,
  isActive: boolean,
) {
  return (await api.patch<{ id: string; isActive: boolean; updatedAt: string }>(
    `/employees/${employeeId}/status`,
    { isActive },
  )).data
}

export async function removeManagementCompanyEmployee(employeeId: string) {
  await api.delete(`/employees/${employeeId}`)
}
export async function hardDeleteManagementCompanyEmployee(employeeId: string) { await api.delete(`/overwatch/management-company-accesses/${employeeId}/hard-delete`, { data: { confirmation: 'EXCLUIR PERMANENTEMENTE' } }) }
export async function hardDeleteManagementCompanyEmployeeEligibility(employeeId: string) { return (await api.get<{ canHardDelete: boolean; reason: string | null }>(`/overwatch/management-company-accesses/${employeeId}/hard-delete-eligibility`)).data }

export async function permanentlyDeleteManagementCompanyRequest(id: string, friendlyIdentifier: string) {
  await api.delete(`/overwatch/management-company-requests/${id}`, { data: { friendlyIdentifier } })
}

export async function resendManagementCompanyAccess(accessId: string) {
  return (await api.post<{ sent: boolean }>(`/overwatch/management-company-accesses/${accessId}/resend-first-access`)).data
}

export async function resetManagementCompanyAccessPassword(accessId: string) {
  return (await api.post<{ email: string; temporaryPassword: string; invitationSent: boolean }>(
    `/overwatch/management-company-accesses/${accessId}/reset-password`,
  )).data
}

export async function setManagementCompanyAccessCategories(accessId: string, categoryIds: string[]) {
  await api.put(`/overwatch/management-company-accesses/${accessId}/categories`, { categoryIds })
}

export async function listManagementCompanyCategories(managementCompanyId: string) {
  return (await api.get<ManagementCompanyCategory[]>(
    `/overwatch/management-companies/${managementCompanyId}/request-categories`,
  )).data
}

export async function setManagementCompanyCategoryStatus(
  managementCompanyId: string, categoryId: string, isActive: boolean,
) {
  return (await api.patch<ManagementCompanyCategory>(
    `/overwatch/management-companies/${managementCompanyId}/request-categories/${categoryId}/status`,
    { isActive },
  )).data
}
