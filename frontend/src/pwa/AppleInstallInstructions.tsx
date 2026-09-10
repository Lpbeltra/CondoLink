import AddToHomeScreenRoundedIcon from '@mui/icons-material/AddToHomeScreenRounded'
import CheckRoundedIcon from '@mui/icons-material/CheckRounded'
import IosShareRoundedIcon from '@mui/icons-material/IosShareRounded'
import MoreHorizRoundedIcon from '@mui/icons-material/MoreHorizRounded'
import { Box, Stack, Typography } from '@mui/material'
import type { ReactNode } from 'react'

function Step({ number, icon, children }: {
  number: number
  icon?: ReactNode
  children: ReactNode
}) {
  return <Stack direction="row" spacing={1} alignItems="center">
    <Box component="span" sx={{ width: 20, flexShrink: 0, textAlign: 'center', fontWeight: 700 }}>
      {number}
    </Box>
    <Box component="span" aria-hidden sx={{ width: 24, flexShrink: 0, lineHeight: 1, textAlign: 'center' }}>
      {icon}
    </Box>
    <Typography variant="body2">{children}</Typography>
  </Stack>
}

export function AppleInstallInstructions({ isIpad = false }: { isIpad?: boolean }) {
  return <Stack spacing={1.25} sx={{ minWidth: 0 }}>
    <Typography fontWeight={750}>
      Instale o Comvy no seu {isIpad ? 'iPad' : 'iPhone'}
    </Typography>
    <Typography variant="body2" color="text.secondary">
      Para usar o Comvy como aplicativo:
    </Typography>
    <Stack spacing={1} color="text.secondary">
      <Step number={1} icon={<MoreHorizRoundedIcon fontSize="small" />}>No Safari, toque em •••.</Step>
      <Step number={2} icon={<IosShareRoundedIcon fontSize="small" />}>Toque em Compartilhar.</Step>
      <Step number={3} icon={<MoreHorizRoundedIcon fontSize="small" />}>Se necessário, toque em Ver Mais.</Step>
      <Step number={4} icon={<AddToHomeScreenRoundedIcon fontSize="small" />}>Escolha Adicionar à Tela de Início.</Step>
      <Step number={5} icon={<CheckRoundedIcon fontSize="small" />}>Confirme em Adicionar.</Step>
    </Stack>
    <Typography variant="body2" color="text.secondary">
      Depois, abra o Comvy pelo ícone criado na Tela de Início.
    </Typography>
  </Stack>
}
