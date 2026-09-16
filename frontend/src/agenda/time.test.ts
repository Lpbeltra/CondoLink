import { describe, expect, it } from 'vitest'
import { classifyAgendaDate, formatAgendaDate } from './time'

describe('agenda temporal helpers', () => {
  const now = new Date('2026-08-20T15:00:00.000Z')

  it('classifies dates using the reminder timezone instead of the browser timezone', () => {
    expect(classifyAgendaDate('2026-08-20T15:00:00.000Z', 'America/Sao_Paulo', now)).toBe('today')
    expect(classifyAgendaDate('2026-08-21T15:00:00.000Z', 'America/Sao_Paulo', now)).toBe('tomorrow')
    expect(classifyAgendaDate('2026-08-19T15:00:00.000Z', 'America/Sao_Paulo', now)).toBe('past')
    expect(classifyAgendaDate('2026-08-22T15:00:00.000Z', 'America/Sao_Paulo', now)).toBe('future')
  })

  it('falls back safely for an invalid timezone and formats valid dates', () => {
    expect(classifyAgendaDate('2026-08-20T15:00:00.000Z', 'invalid/timezone', now)).toBeNull()
    expect(formatAgendaDate('2026-08-20T15:00:00.000Z', 'America/Sao_Paulo')).toContain('2026')
  })
})
