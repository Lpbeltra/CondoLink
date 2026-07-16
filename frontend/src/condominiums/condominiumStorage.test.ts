import { beforeEach, describe, expect, it, vi } from 'vitest'
import { clearStoredCondominiumId, getStoredCondominiumId, resolveCurrentCondominium, storeCondominiumId } from './condominiumStorage'
import { getAccessMessage } from './presentation'
import type { CondominiumContext } from './types'

const contexts: CondominiumContext[] = [
  { membershipId: 'membership-1', condominium: { id: 'central', name: 'Condomínio Central', isActive: true }, roles: ['Resident'], joinedAt: '2026-07-16', membershipActive: true },
  { membershipId: 'membership-2', condominium: { id: 'monticello', name: 'Condomínio Monticello', isActive: true }, roles: ['Manager', 'Resident'], joinedAt: '2026-07-16', membershipActive: true },
]

describe('condominium selection', () => {
  beforeEach(() => {
    const values = new Map<string, string>()
    vi.stubGlobal('localStorage', {
      getItem: (key: string) => values.get(key) ?? null,
      setItem: (key: string, value: string) => values.set(key, value),
      removeItem: (key: string) => values.delete(key),
    })
  })

  it('restores a valid saved condominium', () => {
    expect(resolveCurrentCondominium(contexts, 'monticello')?.condominium.id).toBe('monticello')
  })

  it('falls back to the first context when the saved id is invalid', () => {
    expect(resolveCurrentCondominium(contexts, 'removed')?.condominium.id).toBe('central')
  })

  it('returns null for an empty context list', () => {
    expect(resolveCurrentCondominium([], 'central')).toBeNull()
  })

  it('stores only the selected id and clears it on logout', () => {
    storeCondominiumId('central')
    expect(getStoredCondominiumId()).toBe('central')
    clearStoredCondominiumId()
    expect(getStoredCondominiumId()).toBeNull()
  })
})

describe('role presentation', () => {
  it.each([
    [true, true, 'Você possui acesso como morador e gestor.'],
    [true, false, 'Você possui acesso à gestão deste condomínio.'],
    [false, true, 'Você pode acompanhar suas solicitações por aqui.'],
    [false, false, 'Seu acesso ao condomínio está ativo.'],
  ])('describes manager=%s resident=%s', (manager, resident, message) => {
    expect(getAccessMessage(manager, resident)).toBe(message)
  })
})
