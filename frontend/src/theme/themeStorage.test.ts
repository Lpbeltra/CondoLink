import { beforeEach, describe, expect, it } from 'vitest'
import {
  getStoredPreference,
  isThemePreference,
  nextPreference,
  resolveThemeMode,
  storePreference,
} from './themeStorage'

describe('theme preference storage', () => {
  beforeEach(() => localStorage.clear())

  it('defaults to following the system when nothing is stored', () => {
    expect(getStoredPreference()).toBe('system')
  })

  it('round-trips each valid preference', () => {
    for (const preference of ['light', 'dark', 'system'] as const) {
      storePreference(preference)
      expect(getStoredPreference()).toBe(preference)
    }
  })

  it('falls back to system when the stored value is corrupt', () => {
    localStorage.setItem('condolink.themePreference', 'solarized')
    expect(getStoredPreference()).toBe('system')
  })

  it('validates preference values', () => {
    expect(isThemePreference('light')).toBe(true)
    expect(isThemePreference('dark')).toBe(true)
    expect(isThemePreference('system')).toBe(true)
    expect(isThemePreference('sepia')).toBe(false)
    expect(isThemePreference(null)).toBe(false)
    expect(isThemePreference(undefined)).toBe(false)
  })
})

describe('resolveThemeMode', () => {
  it('follows the OS when the preference is system', () => {
    expect(resolveThemeMode('system', true)).toBe('dark')
    expect(resolveThemeMode('system', false)).toBe('light')
  })

  it('ignores the OS when the user chose explicitly', () => {
    expect(resolveThemeMode('light', true)).toBe('light')
    expect(resolveThemeMode('dark', false)).toBe('dark')
  })
})

describe('nextPreference', () => {
  it('cycles system -> light -> dark -> system', () => {
    expect(nextPreference('system')).toBe('light')
    expect(nextPreference('light')).toBe('dark')
    expect(nextPreference('dark')).toBe('system')
  })

  it('returns to the starting point after three steps', () => {
    const start = 'system' as const
    expect(nextPreference(nextPreference(nextPreference(start)))).toBe(start)
  })
})
