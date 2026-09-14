import { api } from '../services/api'

export interface Employee {
  id: string
  condominiumId: string
  fullName: string
  cpf: string | null
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
  condominiumId: string
  fullName: string
  jobTitle: string
  phoneNumber: string
  email: string
  registrationNumber: string
  admissionDate: string
  cpf?: string
}

function toRequestBody(input: EmployeeInput) {
  return {
    condominiumId: input.condominiumId,
    fullName: input.fullName,
    cpf: input.cpf || null,
    jobTitle: input.jobTitle || null,
    phoneNumber: input.phoneNumber || null,
    email: input.email || null,
    registrationNumber: input.registrationNumber || null,
    admissionDate: input.admissionDate || null,
  }
}

export async function listEmployees(params: { condominiumId?: string; search?: string; status?: string; jobTitle?: string; revealCpf?: boolean }) {
  return (await api.get<Employee[]>('/administrator/employees', { params })).data
}

// Full, unmasked CPF — only ever used to populate the edit form. The list
// endpoint's default response (and its revealCpf=true bulk variant) stay
// separate so this is the one deliberate place a single employee's complete
// CPF is fetched.
export async function getEmployee(employeeId: string) {
  return (await api.get<Employee>(`/administrator/employees/${employeeId}`)).data
}

export async function createEmployee(input: EmployeeInput) {
  return (await api.post<Employee>('/administrator/employees', toRequestBody(input))).data
}

export async function updateEmployee(employeeId: string, input: EmployeeInput) {
  return (await api.put<Employee>(`/administrator/employees/${employeeId}`, toRequestBody(input))).data
}

export async function setEmployeeStatus(employeeId: string, isActive: boolean) {
  return (await api.patch<Employee>(`/administrator/employees/${employeeId}/status`, { isActive })).data
}
