import type { CondominiumContext } from './types'

const currentCondominiumKey = 'condolink.currentCondominiumId'

export function getStoredCondominiumId() {
  return localStorage.getItem(currentCondominiumKey)
}

export function storeCondominiumId(id: string) {
  localStorage.setItem(currentCondominiumKey, id)
}

export function clearStoredCondominiumId() {
  localStorage.removeItem(currentCondominiumKey)
}

export function resolveCurrentCondominium(
  condominiums: CondominiumContext[],
  storedId: string | null,
) {
  if (condominiums.length === 0) return null
  return condominiums.find((item) => item.condominium.id === storedId) ?? condominiums[0]
}
