import type { CondominiumManager, OverwatchManager } from '../managers/types'

export const condominiumManagerCopy = {
  sectionTitle: 'Síndico vinculado',
  emptyTitle: 'Este condomínio ainda não possui síndico vinculado.',
  linkAction: 'Vincular síndico',
  replaceAction: 'Trocar síndico',
  unlinkAction: 'Desvincular',
  unlinkConfirmation:
    'O usuário, seus demais vínculos e outros papéis serão preservados.',
  replaceConfirmation:
    'Os vínculos dele com outros condomínios e seus outros papéis serão preservados.',
} as const

export function eligibleManagers(
  managers: OverwatchManager[],
  current: CondominiumManager | null,
  search: string,
) {
  const term = search.trim().toLocaleLowerCase()
  return managers
    .filter((item) => item.isActive && item.id !== current?.userId)
    .filter((item) =>
      `${item.fullName} ${item.email} ${item.phoneNumber ?? ''}`
        .toLocaleLowerCase()
        .includes(term))
}
