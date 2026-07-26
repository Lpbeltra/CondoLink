import DashboardRoundedIcon from '@mui/icons-material/DashboardRounded'
import ApartmentRoundedIcon from '@mui/icons-material/ApartmentRounded'
import BusinessRoundedIcon from '@mui/icons-material/BusinessRounded'
import SupervisorAccountRoundedIcon from '@mui/icons-material/SupervisorAccountRounded'

export const overwatchNavigationItems = [
  { label: 'Dashboard', path: '/overwatch', icon: DashboardRoundedIcon },
  { label: 'Condomínios', path: '/overwatch/condominiums', icon: ApartmentRoundedIcon },
  { label: 'Administradoras', path: '/overwatch/management-companies', icon: BusinessRoundedIcon },
  { label: 'Síndicos', path: '/overwatch/managers', icon: SupervisorAccountRoundedIcon },
]

export function getOverwatchSelectedPath(pathname: string) {
  return overwatchNavigationItems
    .slice(1)
    .find((item) => pathname.startsWith(item.path))?.path ?? '/overwatch'
}
