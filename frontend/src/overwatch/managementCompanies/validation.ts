import type { EmployeeInput, ManagementCompanyInput } from './types'

const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

export function validateManagementCompany(input: ManagementCompanyInput) {
  if (!input.name.trim()) return 'Informe o nome da administradora.'
  if (input.name.trim().length > 150) return 'O nome deve possuir no máximo 150 caracteres.'
  if ((input.legalName?.trim().length ?? 0) > 200) return 'A razão social deve possuir no máximo 200 caracteres.'
  if ((input.document?.trim().length ?? 0) > 20) return 'O documento deve possuir no máximo 20 caracteres.'
  if ((input.email?.trim().length ?? 0) > 254) return 'O e-mail deve possuir no máximo 254 caracteres.'
  if (input.email?.trim() && !emailPattern.test(input.email.trim())) return 'Informe um e-mail válido.'
  if ((input.phoneNumber?.trim().length ?? 0) > 30) return 'O telefone deve possuir no máximo 30 caracteres.'
  return null
}

export function validateEmployee(input: EmployeeInput) {
  if (!input.fullName.trim()) return 'Informe o nome completo.'
  if (input.fullName.trim().length > 200) return 'O nome completo deve possuir no máximo 200 caracteres.'
  if (!input.email.trim()) return 'Informe o e-mail.'
  if (input.email.trim().length > 254) return 'O e-mail deve possuir no máximo 254 caracteres.'
  if (!emailPattern.test(input.email.trim())) return 'Informe um e-mail válido.'
  return null
}

export function normalizeOptional(value: string) {
  return value.trim() || null
}
