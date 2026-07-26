import type { ManagerInput } from './types'

export function validateManager(input: ManagerInput) {
  if (!input.fullName.trim()) return 'Informe o nome completo.'
  if (input.fullName.trim().length > 200) {
    return 'O nome deve possuir no máximo 200 caracteres.'
  }
  if (!input.email.trim()) return 'Informe o e-mail.'
  if (input.email.trim().length > 254 || !input.email.includes('@')) {
    return 'Informe um e-mail válido.'
  }
  return null
}
