import { describe, expect, it } from 'vitest'
import { validateEmployee } from './validation'

describe('management company employee validation', () => {
  it('requires a full name and valid email', () => {
    expect(validateEmployee({ fullName: '', email: 'person@example.com', contact: null, jobTitle: 'Atendimento' }))
      .toBe('Informe o nome completo.')
    expect(validateEmployee({ fullName: 'Person', email: 'invalid', contact: null, jobTitle: 'Atendimento' }))
      .toBe('Informe um e-mail válido.')
  })

  it('accepts valid employee data', () => {
    expect(validateEmployee({
      fullName: 'Maria da Silva',
      email: 'maria@example.com',
      contact: null,
      jobTitle: 'Financeiro',
    })).toBeNull()
  })
})
