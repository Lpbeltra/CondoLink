import { api } from '../../services/api'

export interface DelegatedCondominium {
  condominiumId: string
  name: string
}

export async function listDelegatedCondominiums() {
  return (await api.get<DelegatedCondominium[]>('/administrator/employee-management/condominiums')).data
}
