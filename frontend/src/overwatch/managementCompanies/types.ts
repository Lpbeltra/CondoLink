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
  accessType?: 'Person' | 'Department'
  isActive: boolean
  lastAccessAt: string | null
  categoryIds: string[]
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
  accessType?: 'Person' | 'Department'
  isActive: boolean
  temporaryPassword: string
  invitationSent?: boolean
}

export interface EmployeeInput {
  fullName: string
  email: string
  contact: string | null
  jobTitle: string
  accessType?: 'Person' | 'Department'
}

export interface ManagementCompanyCategory {
  id: string
  managementCompanyId: string
  name: string
  description: string | null
  formType: 'Generic' | 'SupplierPayment' | 'UnitFine' | 'Reimbursement'
  isActive: boolean
  responsibleAccessIds: string[]
  createdAt: string
  updatedAt: string
}
