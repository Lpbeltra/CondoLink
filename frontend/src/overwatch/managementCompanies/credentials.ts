import type { CreatedManagementCompanyEmployee } from './types'

export function employeeCredentialsText(employee: CreatedManagementCompanyEmployee) {
  return [
    'CondoLink',
    '',
    `Nome: ${employee.fullName}`,
    `E-mail: ${employee.email}`,
    `Senha temporária: ${employee.temporaryPassword}`,
  ].join('\n')
}
