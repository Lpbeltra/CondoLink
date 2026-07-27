import { createContext, useContext } from 'react'
import type { ThemeMode } from './tokens'
import type { ThemePreference } from './themeStorage'

export interface ThemeModeContextValue {
  /** What the user chose ('system' follows the OS). */
  preference: ThemePreference
  /** The concrete mode currently rendered. */
  mode: ThemeMode
  setPreference: (preference: ThemePreference) => void
  /** Advances system -> light -> dark -> system. */
  toggle: () => void
}

export const ThemeModeContext = createContext<ThemeModeContextValue | null>(null)

export function useThemeMode() {
  const context = useContext(ThemeModeContext)
  if (!context) {
    throw new Error('useThemeMode must be used within AppThemeProvider.')
  }
  return context
}
