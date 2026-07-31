import type { CreatedManager, OverwatchManager } from './types'
import { temporaryCredentialsWhatsAppText } from '../../auth/temporaryCredentials'

export const managerDetailTabs = [
  { value: 'overview', label: 'Visão geral' },
  { value: 'condominiums', label: 'Condomínios' },
  { value: 'settings', label: 'Configurações' },
] as const

export function managerDetailsPath(id: string) {
  return `/overwatch/managers/${id}`
}

export function managerCredentialsText(manager: CreatedManager) {
  return temporaryCredentialsWhatsAppText(manager)
}

export function upsertManager(
  managers: OverwatchManager[],
  saved: OverwatchManager,
) {
  return [...managers.filter((item) => item.id !== saved.id), saved]
    .sort((left, right) => left.fullName.localeCompare(right.fullName))
}
