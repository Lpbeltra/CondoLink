import type { CondominiumMember } from './types'

export type PersonBadgeColor = 'success' | 'default' | 'warning' | 'info'

export function getPersonBadges(person: CondominiumMember) {
  const badges: { label: string; color: PersonBadgeColor }[] = [
    person.userActive
      ? { label: 'Ativo', color: 'success' }
      : { label: 'Inativo', color: 'default' },
  ]
  if (!person.lastLoginAt) {
    badges.push({ label: 'Nunca acessou', color: 'info' })
  }
  if (person.mustChangePassword) {
    badges.push({ label: 'Senha temporária', color: 'warning' })
  }
  if (!person.membershipActive) {
    badges.push({ label: 'Vínculo encerrado', color: 'default' })
  }
  return badges
}
