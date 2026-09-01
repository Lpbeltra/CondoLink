import { brazilianStates, digits, isValidCnpj } from '../registration'
import type { EmployeeInput, ManagementCompanyInput } from './types'

const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

export function validateManagementCompany(input: ManagementCompanyInput) {
  if (!input.name.trim()) return 'Informe o nome da administradora.'
  if (input.name.trim().length > 150) return 'O nome deve possuir no máximo 150 caracteres.'
  if (!isValidCnpj(input.cnpj)) return 'Informe um CNPJ válido.'
  if (!input.address.trim()) return 'Informe o endereço.'
  if (input.address.trim().length > 200) return 'O endereço deve possuir no máximo 200 caracteres.'
  if (!input.city.trim()) return 'Informe a cidade.'
  if (input.city.trim().length > 100) return 'A cidade deve possuir no máximo 100 caracteres.'
  if (!brazilianStates.includes(input.state as typeof brazilianStates[number]))
    return 'Selecione um estado válido.'
  if ((input.email?.trim().length ?? 0) > 254) return 'O e-mail deve possuir no máximo 254 caracteres.'
  if (input.email?.trim() && !emailPattern.test(input.email.trim())) return 'Informe um e-mail válido.'
  if ((input.phoneNumber?.trim().length ?? 0) > 30) return 'O telefone deve possuir no máximo 30 caracteres.'
  return null
}

export function validateEmployee(input: EmployeeInput) {
  if (!input.fullName.trim()) return 'Informe o nome completo.'
  if (input.fullName.trim().length > 200) return 'O nome completo deve possuir no máximo 200 caracteres.'
  if (!input.email.trim()) return 'Informe o e-mail.'
  if (input.contact?.trim() && !/^[0-9+ ()\-./]+$/.test(input.contact.trim())) return 'Contato deve ser telefone valido.'
  if (input.email.trim().length > 254 || !emailPattern.test(input.email.trim()))
    return 'Informe um e-mail válido.'
  if ((input.contact?.trim().length ?? 0) > 30) return 'O contato deve possuir no máximo 30 caracteres.'
  if (!input.jobTitle.trim()) return 'Informe a função.'
  if (input.jobTitle.trim().length > 100) return 'A função deve possuir no máximo 100 caracteres.'
  return null
}

export function normalizeOptional(value: string) { return value.trim() || null }
export function normalizeCnpj(value: string) { return digits(value) }
