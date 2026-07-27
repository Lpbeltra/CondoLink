import { describe, expect, it } from 'vitest'
import {
  describeWindow,
  formatDayLabel,
  formatHours,
  formatPercent,
  isEmptyReport,
  priorityLabel,
  toBarHeights,
  topCategories,
  usedPriorities,
} from './presentation'
import type { RequestReport } from './types'

function report(overrides: Partial<RequestReport> = {}): RequestReport {
  return {
    period: { from: '2026-03-01', to: '2026-03-30', days: 30 },
    summary: {
      total: 10,
      open: 4,
      awaitingFirstResponse: 2,
      averageFirstResponseHours: 3,
      averageResolutionHours: 24,
      resolutionRatePercent: 60,
    },
    byCategory: [],
    byPriority: [],
    createdPerDay: [],
    ...overrides,
  }
}

describe('formatHours', () => {
  it('renders an em dash for missing data instead of zero', () => {
    // "0h" would claim instant resolution where none happened.
    expect(formatHours(null)).toBe('—')
  })

  it('renders sub-hour values in minutes', () => {
    expect(formatHours(0.5)).toBe('30 min')
    expect(formatHours(0.25)).toBe('15 min')
  })

  it('renders hours below a day', () => {
    expect(formatHours(3)).toBe('3 h')
    expect(formatHours(3.5)).toBe('3,5 h')
  })

  it('renders days at or above 24 hours', () => {
    expect(formatHours(24)).toBe('1 d')
    expect(formatHours(36)).toBe('1,5 d')
  })

  it('distinguishes a real zero from missing data', () => {
    expect(formatHours(0)).toBe('0 min')
    expect(formatHours(0)).not.toBe(formatHours(null))
  })
})

describe('formatPercent', () => {
  it('renders an em dash when there is nothing to measure', () => {
    expect(formatPercent(null)).toBe('—')
  })

  it('renders whole and fractional percentages', () => {
    expect(formatPercent(60)).toBe('60%')
    expect(formatPercent(66.7)).toBe('66,7%')
    expect(formatPercent(0)).toBe('0%')
    expect(formatPercent(100)).toBe('100%')
  })
})

describe('describeWindow', () => {
  it('labels the standard windows', () => {
    expect(describeWindow(7)).toBe('Últimos 7 dias')
    expect(describeWindow(30)).toBe('Últimos 30 dias')
    expect(describeWindow(90)).toBe('Últimos 90 dias')
  })

  it('falls back to a generic label', () => {
    expect(describeWindow(45)).toBe('Últimos 45 dias')
  })
})

describe('toBarHeights', () => {
  it('scales values against the peak', () => {
    const heights = toBarHeights([
      { day: '2026-03-01', created: 5 },
      { day: '2026-03-02', created: 10 },
      { day: '2026-03-03', created: 0 },
    ])
    expect(heights).toEqual([50, 100, 0])
  })

  it('returns zeros instead of NaN for an all-zero series', () => {
    const heights = toBarHeights([
      { day: '2026-03-01', created: 0 },
      { day: '2026-03-02', created: 0 },
    ])
    expect(heights).toEqual([0, 0])
    expect(heights.every(Number.isFinite)).toBe(true)
  })

  it('handles an empty series', () => {
    expect(toBarHeights([])).toEqual([])
  })
})

describe('isEmptyReport', () => {
  it('treats a null report as empty', () => {
    expect(isEmptyReport(null)).toBe(true)
  })

  it('treats a zero-total report as empty', () => {
    expect(isEmptyReport(report({ summary: { ...report().summary, total: 0 } }))).toBe(true)
  })

  it('treats a populated report as non-empty', () => {
    expect(isEmptyReport(report())).toBe(false)
  })
})

describe('topCategories', () => {
  const many = Array.from({ length: 9 }, (_, index) => ({
    categoryId: `c${index}`,
    name: `Categoria ${index}`,
    total: 9 - index,
    open: 1,
    averageResolutionHours: null,
  }))

  it('caps the list and reports how many were hidden', () => {
    const { visible, hidden } = topCategories(report({ byCategory: many }), 6)
    expect(visible).toHaveLength(6)
    expect(hidden).toBe(3)
  })

  it('hides nothing when the list fits', () => {
    const { visible, hidden } = topCategories(report({ byCategory: many.slice(0, 3) }), 6)
    expect(visible).toHaveLength(3)
    expect(hidden).toBe(0)
  })
})

describe('usedPriorities', () => {
  it('drops priorities with no requests', () => {
    const result = usedPriorities(report({
      byPriority: [
        { priority: 'Low', total: 0, open: 0 },
        { priority: 'Normal', total: 4, open: 2 },
      ],
    }))
    expect(result.map((item) => item.priority)).toEqual(['Normal'])
  })
})

describe('priorityLabel', () => {
  it('translates known priorities', () => {
    expect(priorityLabel('Urgent')).toBe('Urgente')
    expect(priorityLabel('High')).toBe('Alta')
    expect(priorityLabel('Normal')).toBe('Normal')
    expect(priorityLabel('Low')).toBe('Baixa')
  })

  it('passes through an unknown value rather than blanking it', () => {
    expect(priorityLabel('Whatever')).toBe('Whatever')
  })
})

describe('formatDayLabel', () => {
  it('renders day/month', () => {
    expect(formatDayLabel('2026-03-05')).toBe('05/03')
  })

  it('passes through an unexpected shape', () => {
    expect(formatDayLabel('nope')).toBe('nope')
  })
})
