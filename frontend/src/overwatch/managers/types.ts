export interface OverwatchManager {
  id: string
  fullName: string
  email: string
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
  isActive: boolean
  joinedAt: string
}
