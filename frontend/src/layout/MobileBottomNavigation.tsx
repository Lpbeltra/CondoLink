import { BottomNavigation, BottomNavigationAction, Paper } from '@mui/material'
import { useLocation, useNavigate } from 'react-router-dom'
import { getMobileNavigationItems, getMobileSelectedPath } from './navigation'
import { useCondominium } from '../condominiums/CondominiumContext'

export function MobileBottomNavigation() {
  const navigate = useNavigate()
  const location = useLocation()
  const { currentCondominium } = useCondominium()
  const navigationItems = getMobileNavigationItems(currentCondominium?.roles ?? [])
  const selectedPath = getMobileSelectedPath(location.pathname)
  return (
    <Paper elevation={0} sx={{ display: { md: 'none' }, position: 'fixed', zIndex: 1200, bottom: 0, left: 0, right: 0, borderTop: '1px solid', borderColor: 'divider', pb: 'env(safe-area-inset-bottom)', borderRadius: 0 }}>
      <BottomNavigation value={selectedPath} onChange={(_, value) => navigate(value)} showLabels>
        {navigationItems.map(({ label, path, icon: Icon }) => <BottomNavigationAction key={path} label={label} value={path} icon={<Icon />} />)}
      </BottomNavigation>
    </Paper>
  )
}
