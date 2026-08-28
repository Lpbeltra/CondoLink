export type CondominiumRole = 'Manager' | 'SubManager' | 'Resident'

export interface CondominiumContext {
  membershipId: string
  condominium: {
    id: string
    name: string
    isActive: boolean
  }
  roles: CondominiumRole[]
  joinedAt: string
  membershipActive: boolean
}
