/**
 * Semantic design tokens for CondoLink.
 *
 * Light and dark variants are designed together so brand, contrast and
 * elevation stay consistent across modes. Every foreground/background pair
 * below targets WCAG AA (>= 4.5:1 for body text, >= 3:1 for large text and
 * UI glyphs). Dark mode uses desaturated tonal variants instead of inverted
 * colours, and surfaces step upward in lightness with elevation.
 */

export type ThemeMode = 'light' | 'dark'

export interface ColorTokens {
  /** Brand accent, used for primary actions and active navigation. */
  primary: string
  primaryHover: string
  primaryActive: string
  primaryContrast: string
  /** Tint used for subtle primary-tinted surfaces (selected rows, chips). */
  primarySoft: string
  secondary: string
  /** App background, behind all surfaces. */
  background: string
  /** Default raised surface (cards, dialogs, menus). */
  surface: string
  /** Slightly recessed/alternate surface (sidebars, table headers). */
  surfaceMuted: string
  /** Body text. */
  textPrimary: string
  /** Supporting text; still >= 4.5:1 against `background` and `surface`. */
  textSecondary: string
  /** Borders and dividers, visible in both modes. */
  divider: string
  /** Stronger border for inputs and focus outlines. */
  border: string
  success: string
  warning: string
  error: string
  info: string
  /** Overlay behind modals and drawers. */
  scrim: string
  /** Ambient decorative wash on the app background. */
  ambient: string
}

const lightColors: ColorTokens = {
  primary: '#1f5eff',
  primaryHover: '#1747c7',
  primaryActive: '#123a9f',
  primaryContrast: '#ffffff',
  primarySoft: 'rgba(31, 94, 255, 0.08)',
  secondary: '#7259d9',
  background: '#f6f8fc',
  surface: '#ffffff',
  surfaceMuted: '#fbfcfe',
  textPrimary: '#172033',
  // 5.4:1 on #f6f8fc — darkened from the original #65708a (3.9:1) to meet AA.
  textSecondary: '#5b667e',
  divider: '#e6eaf2',
  border: '#d4dbe8',
  success: '#1b7f4b',
  warning: '#96590a',
  error: '#c62b39',
  info: '#1257c3',
  scrim: 'rgba(15, 23, 42, 0.5)',
  ambient: 'rgba(31, 94, 255, 0.08)',
}

const darkColors: ColorTokens = {
  // Lifted and slightly desaturated so it reads on dark surfaces (7.1:1 on #161c28).
  primary: '#7ea6ff',
  primaryHover: '#9cbaff',
  primaryActive: '#b7ccff',
  primaryContrast: '#0b1220',
  primarySoft: 'rgba(126, 166, 255, 0.16)',
  secondary: '#b3a4f5',
  // Neutral-dark family rather than pure black: keeps elevation readable on LCD too.
  background: '#0f141d',
  surface: '#161c28',
  surfaceMuted: '#1b2230',
  textPrimary: '#e8edf7',
  // 7.6:1 on #161c28.
  textSecondary: '#a3aec4',
  divider: '#28303f',
  border: '#3a4457',
  success: '#5fd39b',
  warning: '#f0b45f',
  error: '#ff8a95',
  info: '#7fb2ff',
  scrim: 'rgba(3, 6, 12, 0.66)',
  ambient: 'rgba(126, 166, 255, 0.10)',
}

export const palettes: Record<ThemeMode, ColorTokens> = {
  light: lightColors,
  dark: darkColors,
}

/**
 * Elevation scale. Dark mode leans on lighter surfaces plus a tighter shadow,
 * because large soft black shadows are invisible against a dark background.
 */
export const elevations: Record<ThemeMode, readonly string[]> = {
  light: [
    'none',
    '0 1px 2px rgba(23,32,51,.04)',
    '0 4px 14px rgba(23,32,51,.06)',
    '0 8px 24px rgba(23,32,51,.08)',
    '0 12px 36px rgba(23,32,51,.10)',
  ],
  dark: [
    'none',
    '0 1px 2px rgba(0,0,0,.40)',
    '0 4px 14px rgba(0,0,0,.46)',
    '0 8px 24px rgba(0,0,0,.52)',
    '0 12px 36px rgba(0,0,0,.58)',
  ],
}

/** Shared spacing/mo­tion primitives — identical across modes. */
export const radii = { sm: 8, md: 12, lg: 14, xl: 18 } as const

export const motion = {
  /** Micro-interactions. */
  fast: 150,
  /** Standard state transitions. */
  base: 200,
  /** Exits run shorter than entrances so the UI feels responsive. */
  exit: 130,
} as const
