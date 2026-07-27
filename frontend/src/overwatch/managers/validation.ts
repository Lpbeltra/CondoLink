import { brazilianStates, isValidCnpj, isValidCpf } from '../registration'
import type { ManagerInput } from './types'

export function validateManager(input: ManagerInput) {
  if (!input.fullName.trim()) return 'Informe o nome completo.'
  if (input.fullName.trim().length > 200) return 'O nome deve possuir no máximo 200 caracteres.'
  if (!input.email.trim() || input.email.trim().length > 254 || !input.email.includes('@'))
    return 'Informe um e-mail válido.'
  if ((input.phoneNumber?.trim().length ?? 0) > 30)
    return 'O telefone deve possuir no máximo 30 caracteres.'
  if (input.cpf && !isValidCpf(input.cpf)) return 'Informe um CPF válido.'
  if (input.cnpj && !isValidCnpj(input.cnpj)) return 'Informe um CNPJ válido.'
  if ((input.address?.trim().length ?? 0) > 200)
    return 'O endereço deve possuir no máximo 200 caracteres.'
  if ((input.city?.trim().length ?? 0) > 100)
    return 'A cidade deve possuir no máximo 100 caracteres.'
  if (input.state && !brazilianStates.includes(input.state as typeof brazilianStates[number]))
    return 'Selecione um estado válido.'
  return null
}
