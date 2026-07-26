import { api } from '../../services/api'
import type { OverwatchCondominium } from '../condominiums/types'
import type {
  CondominiumManager,
  CreatedManager,
  ManagerCondominium,
  ManagerInput,
  OverwatchManager,
} from './types'

export async function listManagers() {
  return (await api.get<OverwatchManager[]>('/overwatch/managers')).data
}

export async function getManager(id: string) {
  return (await api.get<OverwatchManager>(`/overwatch/managers/${id}`)).data
}

export async function createManager(input: ManagerInput) {
  return (await api.post<CreatedManager>('/overwatch/managers', input)).data
}

export async function updateManagerStatus(id: string, isActive: boolean) {
  return (await api.patch<{ id: string; isActive: boolean; updatedAt: string }>(
    `/overwatch/managers/${id}/status`,
    { isActive },
  )).data
}

export async function listManagerCondominiums(managerId: string) {
  return (await api.get<ManagerCondominium[]>(
    `/overwatch/managers/${managerId}/condominiums`,
  )).data
}

export async function listCondominiumManagers(condominiumId: string) {
  return (await api.get<CondominiumManager[]>(
    `/overwatch/condominiums/${condominiumId}/managers`,
  )).data
}

export async function listAvailableCondominiums() {
  return (await api.get<OverwatchCondominium[]>('/overwatch/condominiums')).data
}

export async function linkManager(managerId: string, condominiumId: string) {
  return (await api.post<{ membershipId: string }>(
    '/overwatch/management-memberships',
    { managerId, condominiumId },
  )).data
}

export async function removeManagerLink(
  managerId: string,
  condominiumId: string,
) {
  await api.delete(
    `/overwatch/managers/${managerId}/condominiums/${condominiumId}`,
  )
}
