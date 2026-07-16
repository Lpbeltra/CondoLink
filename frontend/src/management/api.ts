import { api } from '../services/api'
import type { Category, CondominiumMember, RelationshipType, Unit, UnitMembership } from './types'

export const listUnits = async (condominiumId: string) => (await api.get<Unit[]>(`/condominiums/${condominiumId}/units`)).data
export const getUnit = async (unitId: string) => (await api.get<Unit>(`/units/${unitId}`)).data
export const createUnit = async (condominiumId: string, payload: { identifier: string; block: string | null; floor: string | null; description: string | null }) => (await api.post<Unit>(`/condominiums/${condominiumId}/units`, payload)).data
export const listUnitMemberships = async (unitId: string) => (await api.get<UnitMembership[]>(`/units/${unitId}/memberships`)).data
export const listCondominiumMembers = async (condominiumId: string) => (await api.get<CondominiumMember[]>(`/condominiums/${condominiumId}/members`)).data
export const createUnitMembership = async (unitId: string, payload: { userId: string; relationshipType: RelationshipType; isResident: boolean; isPrimaryResidence: boolean }) => (await api.post(`/units/${unitId}/memberships`, payload)).data
export const listCategories = async (condominiumId: string) => (await api.get<Category[]>(`/condominiums/${condominiumId}/categories`)).data
export const createCategory = async (condominiumId: string, payload: { name: string; description: string | null }) => (await api.post<Category>(`/condominiums/${condominiumId}/categories`, payload)).data
