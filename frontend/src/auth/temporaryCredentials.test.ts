import { describe, expect, it } from 'vitest'
import { temporaryCredentialsWhatsAppText } from './temporaryCredentials'

describe('temporary credentials WhatsApp message', () => {
  it('keeps the password unchanged and isolated between backticks', () => {
    const text = temporaryCredentialsWhatsAppText({
      fullName: 'Maria Silva',
      email: 'maria@example.com',
      temporaryPassword: 'AbC!9 xY',
    }, 'https://app.example/')

    expect(text).toContain('Olá, Maria Silva!')
    expect(text).toContain('E-mail: maria@example.com')
    expect(text).toContain('\nSenha temporária:\n`AbC!9 xY`\n')
    expect(text).toContain('https://app.example/login')
  })
})
