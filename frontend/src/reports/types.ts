export interface ReportPeriod {
  from: string
  to: string
  days: number
}

export interface ReportSummary {
  total: number
  open: number
  awaitingFirstResponse: number
  /** Null when no request has been answered yet — distinct from zero. */
  averageFirstResponseHours: number | null
  /** Null when nothing has been resolved yet. */
  averageResolutionHours: number | null
  /** Null when there is nothing to measure. */
  resolutionRatePercent: number | null
}

export interface CategoryVolume {
  categoryId: string
  name: string
  total: number
  open: number
  averageResolutionHours: number | null
}

export interface PriorityVolume {
  priority: string
  total: number
  open: number
}

export interface DailyVolume {
  day: string
  created: number
}

export interface RequestReport {
  period: ReportPeriod
  summary: ReportSummary
  byCategory: CategoryVolume[]
  byPriority: PriorityVolume[]
  createdPerDay: DailyVolume[]
}

/** Windows offered in the UI. */
export const reportWindows = [7, 30, 90] as const
export type ReportWindow = (typeof reportWindows)[number]
