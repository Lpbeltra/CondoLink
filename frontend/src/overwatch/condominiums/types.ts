export interface OverwatchCondominium {
  id: string
  name: string
  email: string | null
  cnpj: string | null
  address: string | null
  city: string | null
  state: string | null
  hasDoorman: boolean
  isRemoteDoorman: boolean
  doormanContact: string | null
  whatsAppUpdatesEnabled: boolean
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
  cnpj: string
  address: string
  city: string
  state: string
  hasDoorman: boolean
  isRemoteDoorman: boolean
  doormanContact: string | null
  whatsAppUpdatesEnabled: boolean
}

export interface ManagementCompanyOption {
  id: string
  name: string
  isActive: boolean
}
