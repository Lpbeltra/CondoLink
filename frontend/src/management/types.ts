export interface Unit { id: string; condominiumId: string; identifier: string; block: string | null; floor: string | null; description: string | null; isActive: boolean; createdAt: string; updatedAt: string }
export type RelationshipType = 'Owner' | 'Tenant' | 'AuthorizedOccupant'
export interface UnitMembership { unitMembershipId: string; userId: string; fullName: string; email: string; phoneNumber: string | null; relationshipType: RelationshipType; isResident: boolean; isPrimaryResidence: boolean; membershipActive: boolean; startedAt: string; endedAt: string | null; createdAt: string }
export interface CondominiumMember { membershipId: string; userId: string; fullName: string; email: string; phoneNumber: string | null; membershipActive: boolean; joinedAt: string; endedAt: string | null; roles: string[] }
export interface Category { id: string; condominiumId: string; name: string; description: string | null }
