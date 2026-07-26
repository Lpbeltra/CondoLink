import type { CondominiumInput } from './types'

const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

export function normalizeOptional(value: string) {
  return value.trim() || null
}

export function validateCondominium(input: CondominiumInput) {
  if (!input.name.trim()) return 'Informe o nome do condomínio.'
  if (input.name.trim().length > 200) return 'O nome deve possuir no máximo 200 caracteres.'
  if ((input.email?.length ?? 0) > 254) return 'O e-mail deve possuir no máximo 254 caracteres.'
  if (input.email && !emailPattern.test(input.email)) return 'Informe um e-mail válido.'
  if ((input.phoneNumber?.length ?? 0) > 30) return 'O telefone deve possuir no máximo 30 caracteres.'
  return null
}
