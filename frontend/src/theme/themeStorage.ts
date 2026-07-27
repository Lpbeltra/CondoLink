import type { ThemeMode } from './tokens'

/** User preference: an explicit mode, or follow the operating system. */
export type ThemePreference = ThemeMode | 'system'

const storageKey = 'condolink.themePreference'

export function isThemePreference(value: unknown): value is ThemePreference {
  return value === 'light' || value === 'dark' || value === 'system'
}

export function getStoredPreference(): ThemePreference {
  try {
    const stored = localStorage.getItem(storageKey)
    return isThemePreference(stored) ? stored : 'system'
  } catch {
    // Private-mode / disabled storage: fall back to following the OS.
    return 'system'
  }
}

export function storePreference(preference: ThemePreference) {
  try {
    localStorage.setItem(storageKey, preference)
  } catch {
    // Persisting is best-effort; the in-memory preference still applies.
  }
}

/** Resolves a preference plus the current OS setting into a concrete mode. */
export function resolveThemeMode(
  preference: ThemePreference,
  prefersDark: boolean,
): ThemeMode {
  if (preference === 'system') return prefersDark ? 'dark' : 'light'
  return preference
}

/** Cycle order for the header toggle: system -> light -> dark -> system. */
export function nextPreference(current: ThemePreference): ThemePreference {
  if (current === 'system') return 'light'
  if (current === 'light') return 'dark'
  return 'system'
}
