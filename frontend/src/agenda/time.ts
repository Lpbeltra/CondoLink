export type AgendaDateBucket = 'past' | 'today' | 'tomorrow' | 'future'

function dateParts(value: Date, timeZone: string) {
  const parts = new Intl.DateTimeFormat('en-CA', {
    timeZone,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).formatToParts(value)
  return [
    parts.find(part => part.type === 'year')?.value,
    parts.find(part => part.type === 'month')?.value,
    parts.find(part => part.type === 'day')?.value,
  ].join('-')
}

export function classifyAgendaDate(
  occurrenceUtc: string,
  timeZone: string,
  now = new Date(),
): AgendaDateBucket | null {
  const occurrence = new Date(occurrenceUtc)
  if (Number.isNaN(occurrence.getTime())) return null
  try {
    const currentDay = dateParts(now, timeZone)
    const occurrenceDay = dateParts(occurrence, timeZone)
    const current = Date.parse(`${currentDay}T00:00:00Z`)
    const target = Date.parse(`${occurrenceDay}T00:00:00Z`)
    const difference = Math.round((target - current) / 86400000)
    if (difference < 0) return 'past'
    if (difference === 0) return 'today'
    if (difference === 1) return 'tomorrow'
    return 'future'
  } catch {
    return null
  }
}

export function formatAgendaDate(value: string, timeZone: string) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return 'Data inválida'
  try {
    return new Intl.DateTimeFormat('pt-BR', {
      dateStyle: 'medium',
      timeStyle: 'short',
      timeZone,
    }).format(date)
  } catch {
    return new Intl.DateTimeFormat('pt-BR', {
      dateStyle: 'medium',
      timeStyle: 'short',
    }).format(date)
  }
}

export function formatAgendaTime(value: string, timeZone: string) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return ''
  try {
    return new Intl.DateTimeFormat('pt-BR', { hour: '2-digit', minute: '2-digit', timeZone }).format(date)
  } catch {
    return new Intl.DateTimeFormat('pt-BR', { hour: '2-digit', minute: '2-digit' }).format(date)
  }
}

export function formatAgendaDay(value: string, timeZone: string) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return ''
  try {
    return new Intl.DateTimeFormat('pt-BR', { day: '2-digit', month: 'short', timeZone }).format(date).replace('.', '')
  } catch {
    return new Intl.DateTimeFormat('pt-BR', { day: '2-digit', month: 'short' }).format(date).replace('.', '')
  }
}
