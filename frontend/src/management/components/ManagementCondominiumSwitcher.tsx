import { useMemo, useState } from 'react'
import ApartmentRoundedIcon from '@mui/icons-material/ApartmentRounded'
import CheckRoundedIcon from '@mui/icons-material/CheckRounded'
import ExpandMoreRoundedIcon from '@mui/icons-material/ExpandMoreRounded'
import {
  Alert,
  Box,
  ButtonBase,
  CircularProgress,
  ListItemIcon,
  ListItemText,
  Menu,
  MenuItem,
  Typography,
} from '@mui/material'
import { useManagementContext } from '../ManagementContext'

export function ManagementCondominiumSwitcher() {
  const {
    condominiums,
    activeCondominiumId,
    isLoading,
    isSwitching,
    error,
    selectCondominium,
  } = useManagementContext()

  const [anchor, setAnchor] = useState<HTMLElement | null>(null)

  const activeCondominium = useMemo(
    () =>
      condominiums.find(
        (condominium) => condominium.id === activeCondominiumId
      ) ?? null,
    [condominiums, activeCondominiumId]
  )

  if (isLoading) {
    return (
      <Box
        display="flex"
        alignItems="center"
        justifyContent="center"
        minHeight={44}
      >
        <CircularProgress size={22} />
      </Box>
    )
  }

  if (error) {
    return <Alert severity="error">{error}</Alert>
  }

  if (!activeCondominium) {
    return (
      <Alert severity="info">
        Nenhum condomínio administrativo selecionado.
      </Alert>
    )
  }

  const hasMultiple = condominiums.length > 1

  const closeAndSelect = async (id: string) => {
    setAnchor(null)

    if (id === activeCondominiumId) return

    await selectCondominium(id)
  }

  return (
    <>
      <ButtonBase
        disabled={isSwitching}
        aria-label={
          hasMultiple
            ? 'Trocar condomínio administrativo'
            : 'Condomínio administrativo'
        }
        aria-haspopup={hasMultiple ? 'menu' : undefined}
        aria-expanded={
          hasMultiple && Boolean(anchor) ? 'true' : undefined
        }
        onClick={(event) => {
          if (hasMultiple) {
            setAnchor(event.currentTarget)
          }
        }}
        sx={{
          minWidth: 0,
          width: '100%',
          minHeight: 44,
          maxWidth: { xs: 180, sm: 300 },
          px: 1.25,
          py: 0.5,
          borderRadius: 2,
          border: '1px solid',
          borderColor: 'divider',
          bgcolor: 'background.paper',
          cursor: hasMultiple ? 'pointer' : 'default',
          '&:hover': {
            bgcolor: hasMultiple
              ? 'background.default'
              : 'background.paper',
          },
        }}
      >
        <ApartmentRoundedIcon
          color="primary"
          sx={{ mr: 1, flexShrink: 0 }}
        />

        <Box minWidth={0} flex={1} textAlign="left">
          <Typography
            fontWeight={750}
            fontSize=".85rem"
            noWrap
          >
            {activeCondominium.name}
          </Typography>
        </Box>

        {isSwitching ? (
          <CircularProgress size={18} />
        ) : (
          hasMultiple && (
            <ExpandMoreRoundedIcon
              sx={{
                ml: 0.5,
                color: 'text.secondary',
                flexShrink: 0,
              }}
            />
          )
        )}
      </ButtonBase>

      <Menu
        anchorEl={anchor}
        open={Boolean(anchor)}
        onClose={() => setAnchor(null)}
        slotProps={{
          paper: {
            sx: {
              mt: 1,
              width: 'min(360px, calc(100vw - 32px))',
              maxHeight: 'min(420px, 70dvh)',
            },
          },
        }}
      >
        <Typography
          px={2}
          pt={1}
          pb={0.75}
          color="text.secondary"
          fontSize=".75rem"
          fontWeight={700}
        >
          CONDOMÍNIOS ADMINISTRADOS
        </Typography>

        {condominiums.map((condominium) => {
          const selected =
            condominium.id === activeCondominiumId

          return (
            <MenuItem
              key={condominium.id}
              selected={selected}
              onClick={() => void closeAndSelect(condominium.id)}
              sx={{
                minHeight: 52,
                mx: 1,
                borderRadius: 2,
              }}
            >
              <ListItemText
                primary={condominium.name}
                primaryTypographyProps={{
                  fontWeight: selected ? 750 : 600,
                  noWrap: true,
                }}
              />

              {selected && (
                <ListItemIcon
                  sx={{
                    minWidth: 'auto',
                    ml: 2,
                    color: 'primary.main',
                  }}
                >
                  <CheckRoundedIcon />
                </ListItemIcon>
              )}
            </MenuItem>
          )
        })}
      </Menu>
    </>
  )
}