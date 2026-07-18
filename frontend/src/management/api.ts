import { api } from '../services/api'
import type {
  Category,
  CondominiumBlock,
  CondominiumMember,
  ManagementContextResponse,
  OnboardResult,
  RelationshipType,
  Unit,
  UnitMembership,
} from './types'

export const listUnits = async (condominiumId: string) => (await api.get<Unit[]>(`/condominiums/${condominiumId}/units`)).data
export const getUnit = async (unitId: string) => (await api.get<Unit>(`/units/${unitId}`)).data
export const createUnit = async (condominiumId: string, payload: { identifier: string; blockId: string | null; floor: string | null; description: string | null }) => (await api.post<Unit>(`/condominiums/${condominiumId}/units`, payload)).data
export const updateUnit = async (condominiumId: string, unitId: string, payload: { identifier: string; blockId: string | null; description: string | null }) => api.put(`/condominiums/${condominiumId}/units/${unitId}`, payload)
export const deleteUnit = async (condominiumId: string, unitId: string) => api.delete(`/condominiums/${condominiumId}/units/${unitId}`)
export const listBlocks = async (condominiumId: string) => (await api.get<CondominiumBlock[]>(`/condominiums/${condominiumId}/blocks`)).data
export const createBlock = async (condominiumId: string, identifier: string) => (await api.post<CondominiumBlock>(`/condominiums/${condominiumId}/blocks`, { identifier })).data
export const updateBlock = async (condominiumId: string, blockId: string, identifier: string) => (await api.put<CondominiumBlock>(`/condominiums/${condominiumId}/blocks/${blockId}`, { identifier })).data
export const deleteBlock = async (condominiumId: string, blockId: string) => api.delete(`/condominiums/${condominiumId}/blocks/${blockId}`)
export const listUnitMemberships = async (unitId: string) => (await api.get<UnitMembership[]>(`/units/${unitId}/memberships`)).data
export const listCondominiumMembers = async (condominiumId: string) => (await api.get<CondominiumMember[]>(`/condominiums/${condominiumId}/members`)).data
export const createUnitMembership = async (unitId: string, payload: { userId: string; relationshipType: RelationshipType; isResident: boolean; isPrimaryResidence: boolean }) => (await api.post(`/units/${unitId}/memberships`, payload)).data
export const updateUnitMembership = async (unitId: string, membershipId: string, payload: { relationshipType: RelationshipType; isResident: boolean; isPrimaryResidence: boolean }) => api.put(`/units/${unitId}/memberships/${membershipId}`, payload)
export const deleteUnitMembership = async (unitId: string, membershipId: string) => api.delete(`/units/${unitId}/memberships/${membershipId}`)
export const listCategories = async (condominiumId: string) => (await api.get<Category[]>(`/condominiums/${condominiumId}/categories`)).data
export const createCategory = async (condominiumId: string, payload: { name: string; description: string | null }) => (await api.post<Category>(`/condominiums/${condominiumId}/categories`, payload)).data
export const updateCategory = async (condominiumId: string, categoryId: string, name: string) => (await api.put<Category>(`/condominiums/${condominiumId}/categories/${categoryId}`, { name })).data
export const deleteCategory = async (condominiumId: string, categoryId: string) => api.delete(`/condominiums/${condominiumId}/categories/${categoryId}`)
export const onboardMember = async (condominiumId: string, payload: { fullName: string; email: string; phoneNumber: string | null; unitId: string | null; relationshipType: RelationshipType | null; isResident: boolean; isPrimaryResidence: boolean }) => (await api.post<OnboardResult>(`/condominiums/${condominiumId}/members/onboard`, payload)).data
export const getManagementContext = async () =>
  (await api.get<ManagementContextResponse>('/management/context')).data

export const setManagementContext = async (condominiumId: string | null) =>
  (
    await api.put<ManagementContextResponse>('/management/context', {
      condominiumId,
    })
  ).data