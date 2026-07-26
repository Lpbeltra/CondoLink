import HomeRoundedIcon from '@mui/icons-material/HomeRounded'
import ForumRoundedIcon from '@mui/icons-material/ForumRounded'
import SupportAgentRoundedIcon from '@mui/icons-material/SupportAgentRounded'
import ApartmentRoundedIcon from '@mui/icons-material/ApartmentRounded'
import MoreHorizRoundedIcon from '@mui/icons-material/MoreHorizRounded'
import AdminPanelSettingsRoundedIcon from '@mui/icons-material/AdminPanelSettingsRounded'
import type { SvgIconComponent } from '@mui/icons-material'
import type { CondominiumRole } from '../condominiums/types'
import type { CondominiumContext } from '../condominiums/types'

interface NavigationItem {
  label: string
  path: string
  icon: SvgIconComponent
  requiredRole?: CondominiumRole
  platformAdminOnly?: boolean
}

export const managementEntryPath = '/management/requests'

const commonItems: NavigationItem[] = [
  { label: 'Início', path: '/', icon: HomeRoundedIcon },
  { label: 'Solicitações', path: '/requests', icon: ForumRoundedIcon },
  { label: 'Atendimento', path: managementEntryPath, icon: SupportAgentRoundedIcon, requiredRole: 'Manager' },
  { label: 'Gestão', path: '/management/units', icon: ApartmentRoundedIcon, requiredRole: 'Manager' },
  { label: 'Overwatch', path: '/overwatch', icon: AdminPanelSettingsRoundedIcon, platformAdminOnly: true },
]

// Roles shape the visible UI only. Every real operation must still be authorized by the API.
export function getNavigationItems(roles: CondominiumRole[], userRoles: string[] = []) {
  return commonItems.filter((item) =>
    (!item.requiredRole || roles.includes(item.requiredRole))
    && (!item.platformAdminOnly || userRoles.includes('PlatformAdmin')))
}

export function getMobileNavigationItems(roles: CondominiumRole[], userRoles: string[] = []) {
  const items = getNavigationItems(roles, userRoles)
  if (!roles.includes('Manager')) return items
  const overwatch = items.find((item) => item.path === '/overwatch')
  return [items[0], items[1], ...(overwatch ? [overwatch] : []), { label: 'Mais', path: '/more', icon: MoreHorizRoundedIcon }]
}

export function getMobileSelectedPath(pathname: string) {
  if (pathname.startsWith('/overwatch')) return '/overwatch'
  if (pathname === '/more' || pathname.startsWith('/management')) return '/more'
  if (pathname.startsWith('/requests')) return '/requests'
  return '/'
}

export function shouldShowGeneralCondominiumSwitcher(pathname: string, condominiums: CondominiumContext[]) {
  if (pathname.startsWith('/management') || pathname.startsWith('/overwatch')) return false
  return condominiums.some(item => item.roles.includes('Resident'))
}
