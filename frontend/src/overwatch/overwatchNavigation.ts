import DashboardRoundedIcon from '@mui/icons-material/DashboardRounded'
import ApartmentRoundedIcon from '@mui/icons-material/ApartmentRounded'
import BusinessRoundedIcon from '@mui/icons-material/BusinessRounded'
import SupervisorAccountRoundedIcon from '@mui/icons-material/SupervisorAccountRounded'
import MonitorHeartRoundedIcon from '@mui/icons-material/MonitorHeartRounded'
import MessageRoundedIcon from '@mui/icons-material/MessageRounded'

export const overwatchNavigationItems = [
  { label: 'Dashboard', path: '/overwatch', icon: DashboardRoundedIcon },
  { label: 'Condomínios', path: '/overwatch/condominiums', icon: ApartmentRoundedIcon },
  { label: 'Administradoras', path: '/overwatch/management-companies', icon: BusinessRoundedIcon },
  { label: 'Sistema', path: '/overwatch/system', icon: MonitorHeartRoundedIcon },
  { label: 'Síndicos', path: '/overwatch/managers', icon: SupervisorAccountRoundedIcon },
  { label: 'Mensagens', path: '/overwatch/messages', icon: MessageRoundedIcon },
]

export function getOverwatchSelectedPath(pathname: string) {
  return overwatchNavigationItems
    .slice(1)
    .find((item) => pathname.startsWith(item.path))?.path ?? '/overwatch'
}
