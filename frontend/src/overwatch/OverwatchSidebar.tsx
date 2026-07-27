import { Drawer, List, ListItemButton, ListItemIcon, ListItemText, Toolbar } from '@mui/material'
import { NavLink } from 'react-router-dom'
import { Brand } from '../components/Brand'
import { drawerWidth } from '../layout/Sidebar'
import { overwatchNavigationItems } from './overwatchNavigation'

export function OverwatchSidebar() {
  return (
    <Drawer
      variant="permanent"
      sx={{
        display: { xs: 'none', md: 'block' },
        width: drawerWidth,
        '& .MuiDrawer-paper': {
          width: drawerWidth,
          borderRight: '1px solid',
          borderColor: 'divider',
          bgcolor: 'background.default',
        },
      }}
    >
      <Toolbar sx={{ minHeight: '72px !important', px: 3 }}><Brand /></Toolbar>
      <List
        component="nav"
        aria-label="Navegação da Overwatch"
        sx={{ px: 1.5, pt: 2 }}
      >
        {overwatchNavigationItems.map(({ label, path, icon: Icon }) => (
          <ListItemButton
            key={path}
            component={NavLink}
            to={path}
            end={path === '/overwatch'}
            sx={{
              borderRadius: 2.5,
              mb: .5,
              color: 'text.secondary',
              '&.active': {
                bgcolor: 'rgba(31,94,255,.09)',
                color: 'primary.main',
              },
              '&:hover': { bgcolor: 'rgba(31,94,255,.06)' },
            }}
          >
            <ListItemIcon sx={{ minWidth: 40, color: 'inherit' }}><Icon /></ListItemIcon>
            <ListItemText
              primary={label}
              primaryTypographyProps={{ fontWeight: 700, fontSize: '.925rem' }}
            />
          </ListItemButton>
        ))}
      </List>
    </Drawer>
  )
}
