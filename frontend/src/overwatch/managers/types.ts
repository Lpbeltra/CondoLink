import type { PixKeyType } from '../components/PixFields'

export interface OverwatchManager {
  id: string
  fullName: string
  email: string
  phoneNumber: string | null
  cpf: string | null
  cnpj: string | null
  address: string | null
  city: string | null
  state: string | null
  pixKeyType?: PixKeyType | null
  pixKey?: string | null
  isActive: boolean
  condominiumCount: number
  createdAt: string
  updatedAt: string
}

export interface CreatedManager extends OverwatchManager {
  temporaryPassword: string
}

export interface ManagerInput {
  fullName: string
  email: string
  phoneNumber: string | null
  cpf: string | null
  cnpj: string | null
  address: string | null
  city: string | null
  state: string | null
  pixKeyType?: PixKeyType | null
  pixKey?: string | null
}

export interface ManagerCondominium {
  membershipId: string
  condominiumId: string
  name: string
  managementCompanyName: string | null
  isActive: boolean
  joinedAt: string
}

export interface CondominiumManager {
  membershipId: string
  userId: string
  fullName: string
  email: string
  phoneNumber: string | null
  isActive: boolean
  joinedAt: string
}
