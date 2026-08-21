import { describe, expect, it } from 'vitest'
import { previewMessage, type OperationalMessageTemplate } from './messages'

describe('operational message preview', () => {
  it('uses safe fictitious data and preserves structural content', () => {
    const value = previewMessage({ prefix: 'Olá, {PrimeiroNome}! {NomeCondominio}', suffix: 'Depois', structuralSuffix: '1 - Sim\n2 - Não' } as OperationalMessageTemplate)
    expect(value).toContain('Olá, Maria! Residencial Exemplo')
    expect(value).toContain('[Mensagem escrita pelo síndico]')
    expect(value).toContain('1 - Sim\n2 - Não')
  })
})
