import { useState } from 'react'
import LogoutRoundedIcon from '@mui/icons-material/LogoutRounded'
import { AppBar, Avatar, Box, IconButton, ListItemIcon, Menu, MenuItem, Toolbar, Tooltip, Typography } from '@mui/material'
import { Brand } from '../components/Brand'
import { useAuth } from '../auth/AuthContext'
import { CondominiumSwitcher } from './CondominiumSwitcher'
import { useCondominium } from '../condominiums/CondominiumContext'

export function AppHeader() {
  const { user, logout } = useAuth()
  const { currentCondominium } = useCondominium()
  const [anchor, setAnchor] = useState<HTMLElement | null>(null)
  const initials = user?.fullName.split(' ').slice(0, 2).map((part) => part[0]).join('').toUpperCase()
  return (
    <AppBar position="fixed" color="inherit" elevation={0} sx={{ zIndex: (theme) => theme.zIndex.drawer + 1, borderBottom: '1px solid', borderColor: 'divider', bgcolor: 'rgba(255,255,255,.9)', backdropFilter: 'blur(16px)' }}>
      <Toolbar sx={{ minHeight: { xs: 64, md: 72 }, px: { xs: 1.5, sm: 2, md: 3 }, minWidth: 0, gap: { xs: 1, sm: 1.5 } }}>
        <Box sx={{ display: { xs: 'flex', md: currentCondominium ? 'none' : 'flex' }, flexShrink: 0 }}><Brand compact={Boolean(currentCondominium)} /></Box>
        <Box minWidth={0} flex="1 1 auto" overflow="hidden"><CondominiumSwitcher /></Box>
        <Typography color="text.secondary" fontSize=".875rem" noWrap sx={{ display: { xs: 'none', xl: 'block' }, maxWidth: 180, flexShrink: 1 }}>{user?.fullName}</Typography>
        <Tooltip title="Conta e sair">
          <IconButton aria-label="Abrir menu do usuário" onClick={(event) => setAnchor(event.currentTarget)} sx={{ p: .5, flex: '0 0 auto', minWidth: 44, minHeight: 44 }}>
            <Avatar sx={{ width: 36, height: 36, bgcolor: 'primary.main', fontSize: '.8rem', fontWeight: 750 }}>{initials}</Avatar>
          </IconButton>
        </Tooltip>
      </Toolbar>
      <Menu anchorEl={anchor} open={Boolean(anchor)} onClose={() => setAnchor(null)} anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }} transformOrigin={{ vertical: 'top', horizontal: 'right' }}>
        <Box px={2} py={1} maxWidth={280}>
          <Typography fontWeight={750} noWrap>{user?.fullName}</Typography>
          <Typography color="text.secondary" fontSize=".8rem" noWrap>{user?.email}</Typography>
        </Box>
        <MenuItem onClick={logout} sx={{ minHeight: 44 }}><ListItemIcon><LogoutRoundedIcon fontSize="small" /></ListItemIcon>Sair</MenuItem>
      </Menu>
    </AppBar>
  )
}
