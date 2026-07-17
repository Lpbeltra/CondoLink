import { describe, expect, it } from 'vitest'
import { getMobileNavigationItems, getMobileSelectedPath, getNavigationItems } from './navigation'

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
})
