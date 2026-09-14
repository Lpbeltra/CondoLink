import { EmployeesManager } from '../../employees/EmployeesManager'
import { PageContainer } from '../../components/PageContainer'

export function AdministratorEmployeeManagementPage() {
  return <PageContainer maxWidth={1200}><EmployeesManager /></PageContainer>
}
