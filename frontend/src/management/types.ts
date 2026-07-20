export interface Unit { id: string; condominiumId: string; identifier: string; blockId: string | null; block: string | null; floor: string | null; description: string | null; isActive: boolean; peopleCount?: number; createdAt: string; updatedAt: string }
export interface CondominiumBlock { id: string; condominiumId: string; identifier: string; unitCount: number; createdAt: string; updatedAt: string }
export type RelationshipType = 'Owner' | 'Tenant' | 'AuthorizedOccupant'
export interface UnitMembership { unitMembershipId: string; userId: string; fullName: string; email: string; phoneNumber: string | null; relationshipType: RelationshipType; isResident: boolean; isPrimaryResidence: boolean; membershipActive: boolean; startedAt: string; endedAt: string | null; createdAt: string }
export interface CondominiumMember { membershipId: string; userId: string; fullName: string; cpf?: string | null; email: string; phoneNumber: string | null; membershipActive: boolean; joinedAt: string; endedAt: string | null; roles: string[] }
export interface Category { id: string; condominiumId: string; name: string; description: string | null; requestCount: number }
export interface OnboardResult { user: { id: string; fullName: string; email: string; phoneNumber: string | null; isActive: boolean }; membership: { id: string; condominiumId: string; isActive: boolean; joinedAt: string }; roles: string[]; unitMembership: { id: string; unitId: string; relationshipType: RelationshipType; isResident: boolean; isPrimaryResidence: boolean } | null; isNewUser: boolean; initialPassword: string | null }
export interface ManagementCondominium {
  id: string
  name: string
  isActive: boolean
}

export interface ManagementContextResponse {
  activeCondominiumId: string | null
  availableCondominiums: ManagementCondominium[]
}
