import { api } from '../../services/api'
import type {
  CondominiumInput,
  ManagementCompanyOption,
  OverwatchCondominium,
} from './types'

export async function listOverwatchCondominiums() {
  return (await api.get<OverwatchCondominium[]>('/overwatch/condominiums')).data
}

export async function getOverwatchCondominium(id: string) {
  return (await api.get<OverwatchCondominium>(
    `/overwatch/condominiums/${id}`,
  )).data
}

export async function createOverwatchCondominium(input: CondominiumInput) {
  return (await api.post<Pick<OverwatchCondominium, 'id'>>(
    '/overwatch/condominiums',
    input,
  )).data
}

export async function updateOverwatchCondominium(
  id: string,
  input: CondominiumInput,
) {
  await api.put(`/overwatch/condominiums/${id}`, input)
}

export async function updateOverwatchCondominiumStatus(
  id: string,
  isActive: boolean,
) {
  await api.patch(`/overwatch/condominiums/${id}/status`, { isActive })
}

export async function setCondominiumManagementCompany(
  condominiumId: string,
  managementCompanyId: string | null,
) {
  await api.put(
    `/overwatch/condominiums/${condominiumId}/management-company`,
    { managementCompanyId },
  )
}

export async function listManagementCompanyOptions() {
  const response = await api.get<ManagementCompanyOption[]>(
    '/overwatch/management-companies',
  )
  return response.data
}
