import ForumRoundedIcon from '@mui/icons-material/ForumRounded'
import SupportAgentRoundedIcon from '@mui/icons-material/SupportAgentRounded'
import ApartmentRoundedIcon from '@mui/icons-material/ApartmentRounded'
import AssessmentRoundedIcon from '@mui/icons-material/AssessmentRounded'
import MoreHorizRoundedIcon from '@mui/icons-material/MoreHorizRounded'
import AdminPanelSettingsRoundedIcon from '@mui/icons-material/AdminPanelSettingsRounded'
import AutoAwesomeRoundedIcon from '@mui/icons-material/AutoAwesomeRounded'
import DescriptionRoundedIcon from '@mui/icons-material/DescriptionRounded'
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

export const managementEntryPath = '/management/dashboard'

const commonItems: NavigationItem[] = [
  { label: 'Dashboard', path: '/management/dashboard', icon: AssessmentRoundedIcon, requiredRole: 'Manager' },
  { label: 'Solicitações', path: '/requests', icon: ForumRoundedIcon },
  { label: 'Atendimento', path: '/management/requests', icon: SupportAgentRoundedIcon, requiredRole: 'Manager' },
  { label: 'Assistente', path: '/management/assistant', icon: AutoAwesomeRoundedIcon, requiredRole: 'Manager' },
  { label: 'Documentos', path: '/management/documents', icon: DescriptionRoundedIcon, requiredRole: 'Manager' },
  { label: 'Gestão', path: '/management/units', icon: ApartmentRoundedIcon, requiredRole: 'Manager' },
  { label: 'Overwatch', path: '/overwatch', icon: AdminPanelSettingsRoundedIcon, platformAdminOnly: true },
]

// Roles shape the visible UI only. Every real operation must still be authorized by the API.
export function getNavigationItems(roles: CondominiumRole[], userRoles: string[] = []) {
  return commonItems.filter((item) =>
    (!item.requiredRole || roles.includes(item.requiredRole))
    && (!item.platformAdminOnly || userRoles.includes('PlatformAdmin'))
  )
}

export function getMobileNavigationItems(roles: CondominiumRole[], userRoles: string[] = []) {
  const items = getNavigationItems(roles, userRoles)
  if (!roles.includes('Manager')) return items
  const primary = items.filter(item =>
    item.path === '/management/dashboard'
    || item.path === '/requests'
    || item.path === '/overwatch')
  return [...primary.slice(0, 3), {
    label: 'Mais',
    path: '/more',
    icon: MoreHorizRoundedIcon,
  }]
}

export function getMobileSelectedPath(pathname: string) {
  if (pathname.startsWith('/overwatch')) return '/overwatch'
  if (pathname.startsWith('/management/dashboard')
    || pathname.startsWith('/management/reports'))
    return '/management/dashboard'
  if (pathname === '/more' || pathname.startsWith('/management')) return '/more'
  if (pathname.startsWith('/requests')) return '/requests'
  return '/'
}

export function shouldShowGeneralCondominiumSwitcher(pathname: string, condominiums: CondominiumContext[]) {
  if (pathname.startsWith('/management') || pathname.startsWith('/overwatch')) return false
  return condominiums.some(item => item.roles.includes('Resident'))
}
