export interface OverwatchCondominium {
  id: string
  name: string
  email: string | null
  phoneNumber: string | null
  isActive: boolean
  createdAt: string
  updatedAt: string
  managementCompanyId: string | null
  managementCompanyName: string | null
  managerCount: number
}

export interface CondominiumInput {
  name: string
  email: string | null
  phoneNumber: string | null
}

export interface ManagementCompanyOption {
  id: string
  name: string
  isActive: boolean
}
