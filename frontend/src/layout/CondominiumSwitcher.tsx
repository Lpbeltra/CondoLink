import { useState } from 'react'
import ApartmentRoundedIcon from '@mui/icons-material/ApartmentRounded'
import CheckRoundedIcon from '@mui/icons-material/CheckRounded'
import ExpandMoreRoundedIcon from '@mui/icons-material/ExpandMoreRounded'
import { Box, ButtonBase, ListItemIcon, ListItemText, Menu, MenuItem, Typography } from '@mui/material'
import { useCondominium } from '../condominiums/CondominiumContext'

export function CondominiumSwitcher() {
  const { condominiums, currentCondominium, selectCondominium } = useCondominium()
  const [anchor, setAnchor] = useState<HTMLElement | null>(null)

  if (!currentCondominium) return null

  const hasMultiple = condominiums.length > 1
  const closeAndSelect = (id: string) => {
    selectCondominium(id)
    setAnchor(null)
  }

  return (
    <>
      <ButtonBase
        aria-label={hasMultiple ? 'Trocar condomínio' : 'Condomínio atual'}
        aria-haspopup={hasMultiple ? 'menu' : undefined}
        aria-expanded={hasMultiple && Boolean(anchor) ? 'true' : undefined}
        onClick={(event) => hasMultiple && setAnchor(event.currentTarget)}
        sx={{ minWidth: 0, width: '100%', minHeight: 44, maxWidth: { xs: 180, sm: 300 }, px: 1.25, py: .5, borderRadius: 2, border: '1px solid', borderColor: 'divider', bgcolor: 'background.paper', cursor: hasMultiple ? 'pointer' : 'default', '&:hover': { bgcolor: hasMultiple ? 'background.default' : 'background.paper' } }}
      >
        <ApartmentRoundedIcon color="primary" sx={{ mr: 1, flexShrink: 0 }} />
        <Box minWidth={0} textAlign="left" flex={1}>
          <Typography fontWeight={750} fontSize=".85rem" noWrap>{currentCondominium.condominium.name}</Typography>
        </Box>
        {hasMultiple && <ExpandMoreRoundedIcon sx={{ ml: .5, color: 'text.secondary', flexShrink: 0 }} />}
      </ButtonBase>
      <Menu anchorEl={anchor} open={Boolean(anchor)} onClose={() => setAnchor(null)} slotProps={{ paper: { sx: { mt: 1, width: 'min(360px, calc(100vw - 32px))', maxHeight: 'min(420px, 70dvh)' } } }}>
        <Typography px={2} pt={1} pb={.75} color="text.secondary" fontSize=".75rem" fontWeight={700}>SEUS CONDOMÍNIOS</Typography>
        {condominiums.map((item) => {
          const selected = item.condominium.id === currentCondominium.condominium.id
          return (
            <MenuItem key={item.membershipId} selected={selected} onClick={() => closeAndSelect(item.condominium.id)} sx={{ minHeight: 52, mx: 1, borderRadius: 2 }}>
              <ListItemText primary={item.condominium.name} primaryTypographyProps={{ fontWeight: selected ? 750 : 600, noWrap: true }} />
              {selected && <ListItemIcon sx={{ minWidth: 'auto', ml: 2, color: 'primary.main' }}><CheckRoundedIcon /></ListItemIcon>}
            </MenuItem>
          )
        })}
      </Menu>
    </>
  )
}
