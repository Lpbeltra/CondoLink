import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { WhatsAppButton } from './WhatsAppButton'
import { hasWhatsAppPhone, normalizeWhatsAppPhone, whatsAppUrl } from './whatsApp'

describe('native WhatsApp conversation links', () => {
  it.each(['(44) 99999-9999', '+55 44 99999-9999', '5544999999999'])('normalizes %s', phone => {
    expect(normalizeWhatsAppPhone(phone)).toBe('5544999999999')
    expect(whatsAppUrl(phone)).toBe('https://wa.me/5544999999999')
  })
  it.each([null, '', 'invalid', '123', '00000000000'])('rejects %s', phone => {
    expect(hasWhatsAppPhone(phone)).toBe(false)
    expect(whatsAppUrl(phone)).toBeNull()
  })
  it('uses a native isolated link without navigating the original document', () => {
    render(<WhatsAppButton phone="44999999999">Chamar no WhatsApp</WhatsAppButton>)
    const link = screen.getByRole('link', { name: 'Chamar no WhatsApp' })
    expect(link).toHaveAttribute('href', 'https://wa.me/5544999999999')
    expect(link).toHaveAttribute('target', '_blank')
    expect(link).toHaveAttribute('rel', 'noopener noreferrer')
  })
  it('encodes an existing optional draft for providers', () => {
    expect(whatsAppUrl('44999999999', 'Olá, prestador')).toBe('https://wa.me/5544999999999?text=Ol%C3%A1%2C%20prestador')
  })
})
