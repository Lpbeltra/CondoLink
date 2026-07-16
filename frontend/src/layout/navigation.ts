import HomeRoundedIcon from '@mui/icons-material/HomeRounded'
import ForumRoundedIcon from '@mui/icons-material/ForumRounded'
import type { SvgIconComponent } from '@mui/icons-material'
import type { CondominiumRole } from '../condominiums/types'

interface NavigationItem {
  label: string
  path: string
  icon: SvgIconComponent
  requiredRole?: CondominiumRole
}

const commonItems: NavigationItem[] = [
  { label: 'Início', path: '/', icon: HomeRoundedIcon },
  { label: 'Solicitações', path: '/requests', icon: ForumRoundedIcon },
]

// Roles shape the visible UI only. Every real operation must still be authorized by the API.
export function getNavigationItems(roles: CondominiumRole[]) {
  return commonItems.filter((item) => !item.requiredRole || roles.includes(item.requiredRole))
}
