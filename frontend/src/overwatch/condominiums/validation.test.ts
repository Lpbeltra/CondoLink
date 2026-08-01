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
      cnpj: '', address: '', city: '', state: '',
      hasDoorman: false, isRemoteDoorman: false, doormanContact: null,
      whatsAppUpdatesEnabled: true,
    })).toBe('Informe o nome do condomínio.')
    expect(validateCondominium({
      name: 'Condomínio',
      email: 'invalid',
      cnpj: '11222333000181', address: 'Rua A', city: 'São Paulo', state: 'SP',
      hasDoorman: false, isRemoteDoorman: false, doormanContact: null,
      whatsAppUpdatesEnabled: true,
    })).toBe('Informe um e-mail válido.')
  })
})
