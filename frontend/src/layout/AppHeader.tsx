import { useState } from 'react'
import LogoutRoundedIcon from '@mui/icons-material/LogoutRounded'
import AdminPanelSettingsRoundedIcon from '@mui/icons-material/AdminPanelSettingsRounded'
import SupportAgentRoundedIcon from '@mui/icons-material/SupportAgentRounded'
import { AppBar, Avatar, Box, IconButton, ListItemIcon, Menu, MenuItem, Toolbar, Tooltip, Typography } from '@mui/material'
import { Brand } from '../components/Brand'
import { useAuth } from '../auth/AuthContext'
import { CondominiumSwitcher } from './CondominiumSwitcher'
import { useCondominium } from '../condominiums/CondominiumContext'
import { useLocation, useNavigate } from 'react-router-dom'
import { shouldShowGeneralCondominiumSwitcher } from './navigation'
import {
  getUserMenuAreaAction,
  runUserMenuAreaAction,
} from './userMenu'

export function AppHeader() {
  const { user, logout } = useAuth()
  const { condominiums } = useCondominium()
  const location = useLocation()
  const navigate = useNavigate()
  const showSwitcher = shouldShowGeneralCondominiumSwitcher(location.pathname, condominiums)
  const areaAction = getUserMenuAreaAction(
    user,
    location.pathname,
    condominiums.some((item) => item.roles.includes('Manager')),
  )
  const [anchor, setAnchor] = useState<HTMLElement | null>(null)
  const initials = user?.fullName.split(' ').slice(0, 2).map((part) => part[0]).join('').toUpperCase()
  return (
    <AppBar position="fixed" color="inherit" elevation={0} sx={{ zIndex: (theme) => theme.zIndex.drawer + 1, borderBottom: '1px solid', borderColor: 'divider', bgcolor: 'rgba(255,255,255,.9)', backdropFilter: 'blur(16px)' }}>
      <Toolbar sx={{ minHeight: { xs: 64, md: 72 }, px: { xs: 1.5, sm: 2, md: 3 }, minWidth: 0, gap: { xs: 1, sm: 1.5 } }}>
        <Box sx={{ display: { xs: 'flex', sm: 'none' }, flexShrink: 0 }}><Brand compact /></Box>
        <Box sx={{ display: { xs: 'none', sm: 'flex' }, flexShrink: 0 }}><Brand /></Box>
        <Box minWidth={0} flex="1 1 auto" overflow="hidden">{showSwitcher && <CondominiumSwitcher />}</Box>
        <Typography color="text.secondary" fontSize=".875rem" noWrap sx={{ display: { xs: 'none', xl: 'block' }, maxWidth: 180, flexShrink: 1 }}>{user?.fullName}</Typography>
        <Tooltip title="Conta e sair">
          <IconButton
            aria-label="Abrir menu do usuário"
            aria-haspopup="menu"
            aria-controls={anchor ? 'user-menu' : undefined}
            aria-expanded={anchor ? 'true' : undefined}
            onClick={(event) => setAnchor(event.currentTarget)}
            sx={{ p: .5, flex: '0 0 auto', minWidth: 44, minHeight: 44 }}
          >
            <Avatar sx={{ width: 36, height: 36, bgcolor: 'primary.main', fontSize: '.8rem', fontWeight: 750 }}>{initials}</Avatar>
          </IconButton>
        </Tooltip>
      </Toolbar>
      <Menu id="user-menu" anchorEl={anchor} open={Boolean(anchor)} onClose={() => setAnchor(null)} anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }} transformOrigin={{ vertical: 'top', horizontal: 'right' }}>
        <Box px={2} py={1} maxWidth={280}>
          <Typography fontWeight={750} noWrap>{user?.fullName}</Typography>
          <Typography color="text.secondary" fontSize=".8rem" noWrap>{user?.email}</Typography>
        </Box>
        {areaAction && (
          <MenuItem
            onClick={() =>
              runUserMenuAreaAction(
                areaAction,
                () => setAnchor(null),
                navigate,
              )}
            sx={{ minHeight: 44 }}
          >
            <ListItemIcon>
              {areaAction.kind === 'overwatch'
                ? <AdminPanelSettingsRoundedIcon fontSize="small" />
                : <SupportAgentRoundedIcon fontSize="small" />}
            </ListItemIcon>
            {areaAction.label}
          </MenuItem>
        )}
        <MenuItem
          onClick={() => {
            setAnchor(null)
            logout()
          }}
          sx={{ minHeight: 44 }}
        >
          <ListItemIcon><LogoutRoundedIcon fontSize="small" /></ListItemIcon>
          Sair
        </MenuItem>
      </Menu>
    </AppBar>
  )
}
