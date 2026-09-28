export function normalizeWhatsAppPhone(phone: string | null | undefined) {
  const digits = (phone ?? '').replace(/\D/g, '')
  return digits.length === 10 || digits.length === 11 ? `55${digits}` : digits
}

export function hasWhatsAppPhone(phone: string | null | undefined) {
  const normalized = normalizeWhatsAppPhone(phone)
  return /^55[1-9]\d{9,10}$/.test(normalized) && !/^(\d)\1+$/.test(normalized.slice(2))
}

export function whatsAppUrl(phone: string | null | undefined, message = '') {
  if (!hasWhatsAppPhone(phone)) return null
  return `https://wa.me/${normalizeWhatsAppPhone(phone)}${message ? `?text=${encodeURIComponent(message)}` : ''}`
}
