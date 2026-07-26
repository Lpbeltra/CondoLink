import type { OverwatchCondominium } from './types'

export const condominiumDetailTabs = [
  { value: 'overview', label: 'Visão geral' },
  { value: 'managers', label: 'Síndicos' },
  { value: 'settings', label: 'Configurações' },
] as const

export function condominiumDetailsPath(id: string) {
  return `/overwatch/condominiums/${id}`
}

export function upsertCondominium(
  condominiums: OverwatchCondominium[],
  saved: OverwatchCondominium,
) {
  return [
    ...condominiums.filter((item) => item.id !== saved.id),
    saved,
  ].sort((left, right) => left.name.localeCompare(right.name))
}
