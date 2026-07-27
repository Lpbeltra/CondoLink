export interface ManagementCompany {
  id: string
  name: string
  cnpj: string | null
  address: string | null
  city: string | null
  state: string | null
  email: string | null
  phoneNumber: string | null
  isActive: boolean
  createdAt: string
  updatedAt: string
  condominiumCount: number
  employeeCount: number
}

export interface ManagementCompanyInput {
  name: string
  cnpj: string
  address: string
  city: string
  state: string
  email: string | null
  phoneNumber: string | null
}

export interface ManagementCompanyEmployee {
  id: string
  managementCompanyId: string
  userId: string
  fullName: string
  email: string
  contact: string | null
  jobTitle: string
  isActive: boolean
  createdAt: string
  updatedAt: string
}

export interface CreatedManagementCompanyEmployee {
  id: string
  managementCompanyId: string
  userId: string
  fullName: string
  email: string
  contact: string | null
  jobTitle: string
  isActive: boolean
  temporaryPassword: string
}

export interface EmployeeInput {
  fullName: string
  email: string
  contact: string | null
  jobTitle: string
}
