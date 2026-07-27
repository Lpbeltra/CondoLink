import { describe, expect, it } from 'vitest'
import {
  overwatchMetricLabels,
  overwatchMetricKeys,
  overwatchShortcuts,
} from '../dashboard'

describe('Overwatch dashboard', () => {
  it('defines metric labels without fabricated numeric values', () => {
    expect(overwatchMetricLabels).toEqual([
      'Condomínios',
      'Administradoras',
      'Síndicos',
      'Funcionários',
    ])
  })

  it('offers direct shortcuts to the three managed areas', () => {
    expect(overwatchShortcuts.map((shortcut) => shortcut.path)).toEqual([
      '/overwatch/management-companies',
      '/overwatch/condominiums',
      '/overwatch/managers',
    ])
  })

  it('maps the four cards to the real aggregate response', () => {
    expect(overwatchMetricKeys).toEqual([
      'condominiumCount',
      'managementCompanyCount',
      'managerCount',
      'employeeCount',
    ])
  })
})
