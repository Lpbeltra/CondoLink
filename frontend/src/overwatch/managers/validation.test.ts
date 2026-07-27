import { describe, expect, it } from 'vitest'
import { validateManager } from './validation'

describe('manager validation', () => {
  const optional = {
    phoneNumber: null, cpf: null, cnpj: null, address: null, city: null, state: null,
  }
  it('requires name and a valid email', () => {
    expect(validateManager({ fullName: ' ', email: 'x@example.com', ...optional }))
      .toBe('Informe o nome completo.')
    expect(validateManager({ fullName: 'Manager', email: 'invalid', ...optional }))
      .toBe('Informe um e-mail válido.')
  })

  it('accepts valid input', () => {
    expect(validateManager({
      fullName: 'Manager', email: 'manager@example.com', ...optional,
    })).toBeNull()
  })
})
