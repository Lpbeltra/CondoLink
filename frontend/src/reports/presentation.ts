import type { DailyVolume, RequestReport } from './types'

/**
 * Formats an hour count for display.
 *
 * Null means "no data yet", which must never render as "0h" — that would claim
 * instant resolution where none happened.
 */
export function formatHours(hours: number | null): string {
  if (hours === null) return '—'
  if (hours < 1) {
    const minutes = Math.round(hours * 60)
    return `${minutes} min`
  }
  if (hours < 24) {
    return `${trimZero(hours)} h`
  }
  const days = hours / 24
  return `${trimZero(days)} d`
}

export function formatPercent(value: number | null): string {
  return value === null ? '—' : `${trimZero(value)}%`
}

function trimZero(value: number): string {
  const rounded = Math.round(value * 10) / 10
  return Number.isInteger(rounded)
    ? String(rounded)
    : rounded.toFixed(1).replace('.', ',')
}

/** Human label for the selected window. */
export function describeWindow(days: number): string {
  if (days === 7) return 'Últimos 7 dias'
  if (days === 30) return 'Últimos 30 dias'
  if (days === 90) return 'Últimos 90 dias'
  return `Últimos ${days} dias`
}

/**
 * Scales a daily series to bar heights in percent.
 *
 * All-zero data yields all-zero heights rather than dividing by zero, so an
 * empty period renders a flat baseline instead of NaN.
 */
export function toBarHeights(series: DailyVolume[]): number[] {
  const peak = series.reduce((max, item) => Math.max(max, item.created), 0)
  if (peak === 0) return series.map(() => 0)
  return series.map((item) => Math.round((item.created / peak) * 100))
}

/** True when there is genuinely nothing to show for the period. */
export function isEmptyReport(report: RequestReport | null): boolean {
  return !report || report.summary.total === 0
}

/**
 * The categories worth surfacing, capped so the panel stays readable.
 * Returns the count that was hidden so the UI can say so instead of
 * silently truncating.
 */
export function topCategories(report: RequestReport, limit = 6) {
  const visible = report.byCategory.slice(0, limit)
  return { visible, hidden: Math.max(0, report.byCategory.length - visible.length) }
}

/** Priorities that actually occurred, so empty rows don't pad the panel. */
export function usedPriorities(report: RequestReport) {
  return report.byPriority.filter((item) => item.total > 0)
}

export const priorityLabels: Record<string, string> = {
  Low: 'Baixa',
  Normal: 'Normal',
  High: 'Alta',
  Urgent: 'Urgente',
}

export function priorityLabel(priority: string): string {
  return priorityLabels[priority] ?? priority
}

/** Short day label (e.g. "05/03") for chart axes. */
export function formatDayLabel(day: string): string {
  const [, month, date] = day.split('-')
  return month && date ? `${date}/${month}` : day
}
