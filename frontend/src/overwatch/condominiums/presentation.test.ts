import { describe, expect, it } from 'vitest'
import {
  condominiumDetailsPath,
  condominiumDetailTabs,
  upsertCondominium,
} from './presentation'
import type { OverwatchCondominium } from './types'

function condominium(id: string, name: string): OverwatchCondominium {
  return {
    id,
    name,
    email: null,
    phoneNumber: null,
    isActive: true,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    managementCompanyId: null,
    managementCompanyName: null,
    managerCount: 0,
  }
}

describe('Overwatch condominium presentation', () => {
  it('adds a created condominium to the ordered list', () => {
    expect(upsertCondominium(
      [condominium('z', 'Zulu')],
      condominium('a', 'Alpha'),
    ).map((item) => item.name)).toEqual(['Alpha', 'Zulu'])
  })

  it('provides the explicit manage destination and detail tabs', () => {
    expect(condominiumDetailsPath('condominium-id'))
      .toBe('/overwatch/condominiums/condominium-id')
    expect(condominiumDetailTabs.map((item) => item.label))
      .toEqual(['Visão geral', 'Síndicos', 'Configurações'])
  })
})
