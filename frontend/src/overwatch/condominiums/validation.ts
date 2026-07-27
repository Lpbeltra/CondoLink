import { brazilianStates, digits, isValidCnpj } from '../registration'
import type { CondominiumInput } from './types'

const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/
export function normalizeOptional(value: string) { return value.trim() || null }
export function normalizeCnpj(value: string) { return digits(value) }

export function validateCondominium(input: CondominiumInput) {
  if (!input.name.trim()) return 'Informe o nome do condomínio.'
  if (input.name.trim().length > 200) return 'O nome deve possuir no máximo 200 caracteres.'
  if ((input.email?.length ?? 0) > 254) return 'O e-mail deve possuir no máximo 254 caracteres.'
  if (input.email && !emailPattern.test(input.email)) return 'Informe um e-mail válido.'
  if (!isValidCnpj(input.cnpj)) return 'Informe um CNPJ válido.'
  if (!input.address.trim()) return 'Informe o endereço.'
  if (input.address.trim().length > 200) return 'O endereço deve possuir no máximo 200 caracteres.'
  if (!input.city.trim()) return 'Informe a cidade.'
  if (input.city.trim().length > 100) return 'A cidade deve possuir no máximo 100 caracteres.'
  if (!brazilianStates.includes(input.state as typeof brazilianStates[number]))
    return 'Selecione um estado válido.'
  if ((input.doormanContact?.length ?? 0) > 100)
    return 'O contato da portaria deve possuir no máximo 100 caracteres.'
  return null
}
