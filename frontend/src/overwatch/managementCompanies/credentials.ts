import type { CreatedManagementCompanyEmployee } from './types'
import { temporaryCredentialsWhatsAppText } from '../../auth/temporaryCredentials'

export function employeeCredentialsText(employee: CreatedManagementCompanyEmployee) {
  const temporaryPassword = employee.temporaryPassword
  if (!temporaryPassword) return `E-mail: ${employee.email}`
  return temporaryCredentialsWhatsAppText({ ...employee, temporaryPassword })
}
