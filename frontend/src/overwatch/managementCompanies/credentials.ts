import type { CreatedManagementCompanyEmployee } from './types'
import { temporaryCredentialsWhatsAppText } from '../../auth/temporaryCredentials'

export function employeeCredentialsText(employee: CreatedManagementCompanyEmployee) {
  return temporaryCredentialsWhatsAppText(employee)
}
