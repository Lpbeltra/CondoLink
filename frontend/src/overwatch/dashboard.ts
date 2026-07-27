export const overwatchMetricLabels = [
  'Condomínios',
  'Administradoras',
  'Síndicos',
  'Funcionários',
] as const

export const overwatchShortcuts = [
  { label: 'Administradoras', path: '/overwatch/management-companies' },
  { label: 'Condomínios', path: '/overwatch/condominiums' },
  { label: 'Síndicos', path: '/overwatch/managers' },
] as const

export interface OverwatchDashboardMetrics {
  managementCompanyCount: number
  condominiumCount: number
  managerCount: number
  employeeCount: number
}

export const overwatchMetricKeys: (keyof OverwatchDashboardMetrics)[] = [
  'condominiumCount',
  'managementCompanyCount',
  'managerCount',
  'employeeCount',
]
