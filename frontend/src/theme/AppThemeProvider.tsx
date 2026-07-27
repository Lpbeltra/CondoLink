import { useCallback, useEffect, useMemo, useState, type PropsWithChildren } from 'react'
import { CssBaseline, ThemeProvider } from '@mui/material'
import { createAppTheme } from './createAppTheme'
import { ThemeModeContext } from './ThemeModeContext'
import { palettes } from './tokens'
import {
  getStoredPreference,
  nextPreference,
  resolveThemeMode,
  storePreference,
  type ThemePreference,
} from './themeStorage'

const darkQuery = '(prefers-color-scheme: dark)'

function systemPrefersDark() {
  return typeof window !== 'undefined'
    && typeof window.matchMedia === 'function'
    && window.matchMedia(darkQuery).matches
}

/** Keeps the PWA/browser chrome colour in step with the rendered theme. */
function syncThemeColorMeta(color: string) {
  if (typeof document === 'undefined') return
  let meta = document.querySelector<HTMLMetaElement>('meta[name="theme-color"]')
  if (!meta) {
    meta = document.createElement('meta')
    meta.name = 'theme-color'
    document.head.appendChild(meta)
  }
  meta.content = color
}

export function AppThemeProvider({ children }: PropsWithChildren) {
  const [preference, setPreferenceState] = useState<ThemePreference>(getStoredPreference)
  const [prefersDark, setPrefersDark] = useState(systemPrefersDark)

  // Track the OS setting so 'system' updates live, without a reload.
  useEffect(() => {
    if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') return
    const query = window.matchMedia(darkQuery)
    const handleChange = (event: MediaQueryListEvent) => setPrefersDark(event.matches)
    query.addEventListener('change', handleChange)
    setPrefersDark(query.matches)
    return () => query.removeEventListener('change', handleChange)
  }, [])

  const mode = resolveThemeMode(preference, prefersDark)
  const theme = useMemo(() => createAppTheme(mode), [mode])

  useEffect(() => {
    syncThemeColorMeta(palettes[mode].background)
    // Lets non-MUI CSS (and the ::selection rule) branch on the active mode.
    document.documentElement.dataset.theme = mode
  }, [mode])

  const setPreference = useCallback((next: ThemePreference) => {
    setPreferenceState(next)
    storePreference(next)
  }, [])

  const toggle = useCallback(() => {
    setPreferenceState((current) => {
      const next = nextPreference(current)
      storePreference(next)
      return next
    })
  }, [])

  const value = useMemo(
    () => ({ preference, mode, setPreference, toggle }),
    [preference, mode, setPreference, toggle],
  )

  return (
    <ThemeModeContext.Provider value={value}>
      <ThemeProvider theme={theme}>
        <CssBaseline />
        {children}
      </ThemeProvider>
    </ThemeModeContext.Provider>
  )
}
