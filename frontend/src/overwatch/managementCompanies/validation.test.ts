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

  it('rejects a contact that is not a phone number', () => {
    expect(validateEmployee({ fullName: 'Thiago Soto', email: 'thiago@dimarp.com', contact: 'Thiago', jobTitle: 'Administrativo' }))
      .toBe('Contato deve ser telefone valido.')
  })

  it('accepts both access types with an optional phone', () => {
    expect(validateEmployee({ fullName: 'Pessoa', email: 'pessoa@example.com', contact: '+55 11 99000-0000', jobTitle: 'Operacao', accessType: 'Person' })).toBeNull()
    expect(validateEmployee({ fullName: 'Setor', email: 'setor@example.com', contact: null, jobTitle: 'Financeiro', accessType: 'Department' })).toBeNull()
  })
})
