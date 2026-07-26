import { describe, expect, it } from 'vitest'
import { validateManager } from './validation'

describe('manager validation', () => {
  it('requires name and a valid email', () => {
    expect(validateManager({ fullName: ' ', email: 'x@example.com' }))
      .toBe('Informe o nome completo.')
    expect(validateManager({ fullName: 'Manager', email: 'invalid' }))
      .toBe('Informe um e-mail válido.')
  })

  it('accepts valid input', () => {
    expect(validateManager({
      fullName: 'Manager', email: 'manager@example.com',
    })).toBeNull()
  })
})
