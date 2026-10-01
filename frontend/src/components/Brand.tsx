import { Box } from '@mui/material'
import { useTheme } from '@mui/material/styles'

interface BrandProps {
  compact?: boolean
  surface?: 'auto' | 'light' | 'dark'
}

export function Brand({ compact = false, surface = 'auto' }: BrandProps) {
  const theme = useTheme()
  const dark = surface === 'dark' || (surface === 'auto' && theme.palette.mode === 'dark')
  const src = compact
    ? '/comvy-symbol.svg'
    : dark ? '/comvy-logo-dark.svg' : '/comvy-logo.svg'

  return (
    <Box
      component="img"
      src={src}
      alt="Comvy"
      role="img"
      sx={{ height: 36, width: 'auto', display: 'block', flexShrink: 0 }}
    />
  )
}
