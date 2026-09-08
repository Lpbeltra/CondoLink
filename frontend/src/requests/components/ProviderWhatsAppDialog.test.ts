import { describe, expect, it } from 'vitest'
import { hasWhatsAppPhone, normalizeWhatsAppPhone, providerWhatsAppUrl } from './ProviderWhatsAppDialog'

describe('provider WhatsApp link', () => {
  it('normalizes Brazilian phones and encodes the reviewed message', () => {
    expect(normalizeWhatsAppPhone('(44) 99999-9999')).toBe('5544999999999')
    expect(providerWhatsAppUrl('(44) 99999-9999', 'Olá César!')).toBe('https://wa.me/5544999999999?text=Ol%C3%A1%20C%C3%A9sar!')
    expect(hasWhatsAppPhone('sem telefone')).toBe(false)
  })
})
