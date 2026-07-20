import HomeRoundedIcon from '@mui/icons-material/HomeRounded'
import ForumRoundedIcon from '@mui/icons-material/ForumRounded'
import SupportAgentRoundedIcon from '@mui/icons-material/SupportAgentRounded'
import ApartmentRoundedIcon from '@mui/icons-material/ApartmentRounded'
import MoreHorizRoundedIcon from '@mui/icons-material/MoreHorizRounded'
import type { SvgIconComponent } from '@mui/icons-material'
import type { CondominiumRole } from '../condominiums/types'
import type { CondominiumContext } from '../condominiums/types'

interface NavigationItem {
  label: string
  path: string
  icon: SvgIconComponent
  requiredRole?: CondominiumRole
}

const commonItems: NavigationItem[] = [
  { label: 'Início', path: '/', icon: HomeRoundedIcon },
  { label: 'Solicitações', path: '/requests', icon: ForumRoundedIcon },
  { label: 'Atendimento', path: '/management/requests', icon: SupportAgentRoundedIcon, requiredRole: 'Manager' },
  { label: 'Gestão', path: '/management/units', icon: ApartmentRoundedIcon, requiredRole: 'Manager' },
]

// Roles shape the visible UI only. Every real operation must still be authorized by the API.
export function getNavigationItems(roles: CondominiumRole[]) {
  return commonItems.filter((item) => !item.requiredRole || roles.includes(item.requiredRole))
}

export function getMobileNavigationItems(roles: CondominiumRole[]) {
  const items = commonItems.filter((item) => !item.requiredRole || roles.includes(item.requiredRole))
  if (!roles.includes('Manager')) return items
  return [items[0], items[1], { label: 'Mais', path: '/more', icon: MoreHorizRoundedIcon }]
}

export function getMobileSelectedPath(pathname: string) {
  if (pathname === '/more' || pathname.startsWith('/management')) return '/more'
  if (pathname.startsWith('/requests')) return '/requests'
  return '/'
}

export function shouldShowGeneralCondominiumSwitcher(pathname: string, condominiums: CondominiumContext[]) {
  if (pathname.startsWith('/management')) return false
  return condominiums.some(item => item.roles.includes('Resident'))
}
