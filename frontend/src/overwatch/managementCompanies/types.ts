export interface ManagementCompany {
  id: string
  name: string
  legalName: string | null
  document: string | null
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
  legalName: string | null
  document: string | null
  email: string | null
  phoneNumber: string | null
}

export interface ManagementCompanyEmployee {
  id: string
  managementCompanyId: string
  userId: string
  fullName: string
  email: string
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
  isActive: boolean
  temporaryPassword: string
}

export interface EmployeeInput {
  fullName: string
  email: string
}
