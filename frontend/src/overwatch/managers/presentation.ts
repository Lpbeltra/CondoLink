import type { CreatedManager, OverwatchManager } from './types'

export const managerDetailTabs = [
  { value: 'overview', label: 'Visão geral' },
  { value: 'condominiums', label: 'Condomínios' },
  { value: 'settings', label: 'Configurações' },
] as const

export function managerDetailsPath(id: string) {
  return `/overwatch/managers/${id}`
}

export function managerCredentialsText(manager: CreatedManager) {
  return `Comvy\nNome: ${manager.fullName}\nE-mail: ${manager.email}\nSenha temporária: ${manager.temporaryPassword}`
}

export function upsertManager(
  managers: OverwatchManager[],
  saved: OverwatchManager,
) {
  return [...managers.filter((item) => item.id !== saved.id), saved]
    .sort((left, right) => left.fullName.localeCompare(right.fullName))
}
