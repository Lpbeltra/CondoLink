import { api } from '../../services/api'

export interface EmployeeManagementCondominium {
  id: string
  name: string
}

export async function listEmployeeManagementCondominiums() {
  return (await api.get<EmployeeManagementCondominium[]>('/administrator/employees/condominiums')).data
}
