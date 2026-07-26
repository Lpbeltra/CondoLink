import { BottomNavigation, BottomNavigationAction, Paper } from '@mui/material'
import { useLocation, useNavigate } from 'react-router-dom'
import { getOverwatchSelectedPath, overwatchNavigationItems } from './overwatchNavigation'

export function OverwatchMobileNavigation() {
  const navigate = useNavigate()
  const location = useLocation()
  return (
    <Paper
      elevation={0}
      sx={{
        display: { md: 'none' },
        position: 'fixed',
        zIndex: 1200,
        bottom: 0,
        left: 0,
        right: 0,
        borderTop: '1px solid',
        borderColor: 'divider',
        pb: 'env(safe-area-inset-bottom)',
        borderRadius: 0,
      }}
    >
      <BottomNavigation
        aria-label="Navegação da Overwatch"
        value={getOverwatchSelectedPath(location.pathname)}
        onChange={(_, value) => navigate(value)}
        showLabels
      >
        {overwatchNavigationItems.map(({ label, path, icon: Icon }) => (
          <BottomNavigationAction
            key={path}
            label={label}
            value={path}
            icon={<Icon />}
          />
        ))}
      </BottomNavigation>
    </Paper>
  )
}
