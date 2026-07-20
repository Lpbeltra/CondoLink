import { describe, expect, it } from 'vitest'
import { getMobileNavigationItems, getMobileSelectedPath, getNavigationItems, shouldShowGeneralCondominiumSwitcher } from './navigation'
import type { CondominiumContext } from '../condominiums/types'

describe('role-based navigation', () => {
  it('shows only common items to residents', () => {
    expect(getNavigationItems(['Resident']).map((item) => item.label)).toEqual(['Início', 'Solicitações'])
    expect(getMobileNavigationItems(['Resident']).map((item) => item.label)).toEqual(['Início', 'Solicitações'])
  })

  it('shows management resources to managers', () => {
    expect(getNavigationItems(['Manager', 'Resident']).map((item) => item.label))
      .toEqual(['Início', 'Solicitações', 'Atendimento', 'Gestão'])
  })

  it('keeps manager mobile navigation compact', () => {
    expect(getMobileNavigationItems(['Manager']).map((item) => item.label))
      .toEqual(['Início', 'Solicitações', 'Mais'])
  })

  it('marks the correct mobile destination for nested routes', () => {
    expect(getMobileSelectedPath('/requests/new')).toBe('/requests')
    expect(getMobileSelectedPath('/management/people')).toBe('/more')
    expect(getMobileSelectedPath('/more')).toBe('/more')
    expect(getMobileSelectedPath('/')).toBe('/')
  })

  it('hides the general condominium switcher in management and for manager-only users', () => {
    const resident: CondominiumContext = { membershipId: '1', condominium: { id: 'c1', name: 'A', isActive: true }, roles: ['Resident'], joinedAt: '', membershipActive: true }
    const manager: CondominiumContext = { membershipId: '2', condominium: { id: 'c2', name: 'B', isActive: true }, roles: ['Manager'], joinedAt: '', membershipActive: true }
    expect(shouldShowGeneralCondominiumSwitcher('/', [resident])).toBe(true)
    expect(shouldShowGeneralCondominiumSwitcher('/', [manager])).toBe(false)
    expect(shouldShowGeneralCondominiumSwitcher('/management/units', [resident, manager])).toBe(false)
  })
})
