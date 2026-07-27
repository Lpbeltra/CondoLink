import { describe, expect, it } from 'vitest'
import type { CondominiumManager, OverwatchManager } from '../managers/types'
import { condominiumManagerCopy, eligibleManagers } from './managerPresentation'

const manager = (
  id: string,
  fullName: string,
  isActive = true,
): OverwatchManager => ({
  id,
  fullName,
  email: `${id}@example.com`,
  phoneNumber: id === 'available' ? '11999999999' : null,
  cpf: null,
  cnpj: null,
  address: null,
  city: null,
  state: null,
  isActive,
  condominiumCount: 0,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
})

const current: CondominiumManager = {
  membershipId: 'membership',
  userId: 'current',
  fullName: 'Atual',
  email: 'current@example.com',
  phoneNumber: null,
  isActive: true,
  joinedAt: '2026-01-01T00:00:00Z',
}

describe('single condominium manager presentation', () => {
  it('defines singular empty, link, replace and unlink copy', () => {
    expect(condominiumManagerCopy.sectionTitle).toBe('Síndico vinculado')
    expect(condominiumManagerCopy.emptyTitle)
      .toBe('Este condomínio ainda não possui síndico vinculado.')
    expect(condominiumManagerCopy.linkAction).toBe('Vincular síndico')
    expect(condominiumManagerCopy.replaceAction).toBe('Trocar síndico')
    expect(condominiumManagerCopy.unlinkAction).toBe('Desvincular')
    expect(condominiumManagerCopy.replaceConfirmation)
      .toContain('outros condomínios')
    expect(condominiumManagerCopy.unlinkConfirmation)
      .toContain('demais vínculos')
  })

  it('excludes the current and inactive managers and searches useful fields', () => {
    const managers = [
      manager('current', 'Atual'),
      manager('inactive', 'Inativo', false),
      manager('available', 'Disponível'),
    ]

    expect(eligibleManagers(managers, current, '')).toEqual([managers[2]])
    expect(eligibleManagers(managers, current, '119999')).toEqual([managers[2]])
    expect(eligibleManagers(managers, current, 'ausente')).toEqual([])
  })
})
