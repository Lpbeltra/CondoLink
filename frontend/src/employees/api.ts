import { api } from '../services/api'

export interface Employee {
  id: string
  condominiumId: string
  fullName: string
  jobTitle: string | null
  phoneNumber: string | null
  email: string | null
  registrationNumber: string | null
  admissionDate: string | null
  isActive: boolean
  createdAt: string
  updatedAt: string
}

export interface EmployeeInput {
  fullName: string
  jobTitle: string
  phoneNumber: string
  email: string
  registrationNumber: string
  admissionDate: string
}

function toRequestBody(input: EmployeeInput) {
  return {
    fullName: input.fullName,
    jobTitle: input.jobTitle || null,
    phoneNumber: input.phoneNumber || null,
    email: input.email || null,
    registrationNumber: input.registrationNumber || null,
    admissionDate: input.admissionDate || null,
  }
}

export async function listEmployees(condominiumId: string, params: { search?: string; status?: string }) {
  return (await api.get<Employee[]>(`/condominiums/${condominiumId}/employees`, { params })).data
}

export async function createEmployee(condominiumId: string, input: EmployeeInput) {
  return (await api.post<Employee>(`/condominiums/${condominiumId}/employees`, toRequestBody(input))).data
}

export async function updateEmployee(condominiumId: string, employeeId: string, input: EmployeeInput) {
  return (await api.put<Employee>(`/condominiums/${condominiumId}/employees/${employeeId}`, toRequestBody(input))).data
}

export async function setEmployeeStatus(condominiumId: string, employeeId: string, isActive: boolean) {
  return (await api.patch<Employee>(`/condominiums/${condominiumId}/employees/${employeeId}/status`, { isActive })).data
}
