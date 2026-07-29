import ArrowBackRoundedIcon from '@mui/icons-material/ArrowBackRounded'
import { Box, Divider, Drawer, List, ListItemButton, ListItemIcon, ListItemText, Toolbar } from '@mui/material'
import { NavLink } from 'react-router-dom'
import { Brand } from '../components/Brand'
import { drawerWidth } from '../layout/Sidebar'
import { overwatchNavigationItems } from './overwatchNavigation'
import { useManagementContext } from '../management/ManagementContext'
import { managementEntryPath } from '../layout/navigation'

export function OverwatchSidebar() {
  const { condominiumCount } = useManagementContext()
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
      <Box display="flex" flexDirection="column" flex={1}>
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
      {condominiumCount > 0 && (
        <Box mt="auto" px={1.5} pb={2}>
          <Divider sx={{ mb: 1.5 }} />
          <ListItemButton component={NavLink} to={managementEntryPath} sx={{ borderRadius: 2.5, color: 'text.secondary' }}>
            <ListItemIcon sx={{ minWidth: 40, color: 'inherit' }}><ArrowBackRoundedIcon /></ListItemIcon>
            <ListItemText primary="Voltar para a gestão" primaryTypographyProps={{ fontWeight: 700, fontSize: '.875rem' }} />
          </ListItemButton>
        </Box>
      )}
      </Box>
    </Drawer>
  )
}
