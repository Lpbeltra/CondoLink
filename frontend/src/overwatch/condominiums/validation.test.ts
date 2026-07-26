import { describe, expect, it } from 'vitest'
import { normalizeOptional, validateCondominium } from './validation'

describe('Overwatch condominium validation', () => {
  it('normalizes empty optional values to null', () => {
    expect(normalizeOptional('   ')).toBeNull()
    expect(normalizeOptional(' value ')).toBe('value')
  })

  it('requires a name and validates email', () => {
    expect(validateCondominium({
      name: '',
      email: null,
      phoneNumber: null,
    })).toBe('Informe o nome do condomínio.')
    expect(validateCondominium({
      name: 'Condomínio',
      email: 'invalid',
      phoneNumber: null,
    })).toBe('Informe um e-mail válido.')
  })
})
