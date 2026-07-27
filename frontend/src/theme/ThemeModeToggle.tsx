import { IconButton, Tooltip } from '@mui/material'
import DarkModeOutlinedIcon from '@mui/icons-material/DarkModeOutlined'
import LightModeOutlinedIcon from '@mui/icons-material/LightModeOutlined'
import SettingsBrightnessOutlinedIcon from '@mui/icons-material/SettingsBrightnessOutlined'
import { useThemeMode } from './ThemeModeContext'
import type { ThemePreference } from './themeStorage'

const labels: Record<ThemePreference, string> = {
  system: 'Tema: seguindo o sistema',
  light: 'Tema: claro',
  dark: 'Tema: escuro',
}

const nextLabels: Record<ThemePreference, string> = {
  system: 'Alternar para o tema claro',
  light: 'Alternar para o tema escuro',
  dark: 'Alternar para o tema do sistema',
}

export function ThemeModeToggle() {
  const { preference, toggle } = useThemeMode()

  const Icon = preference === 'system'
    ? SettingsBrightnessOutlinedIcon
    : preference === 'light'
      ? LightModeOutlinedIcon
      : DarkModeOutlinedIcon

  return (
    <Tooltip title={labels[preference]}>
      <IconButton
        onClick={toggle}
        color="inherit"
        // Icon-only control: the accessible name must describe the action.
        aria-label={nextLabels[preference]}
        sx={{ minWidth: 44, minHeight: 44 }}
      >
        <Icon fontSize="small" />
      </IconButton>
    </Tooltip>
  )
}
