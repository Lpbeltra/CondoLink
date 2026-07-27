import { createTheme, type Theme } from '@mui/material/styles'
import { elevations, motion, palettes, radii, type ThemeMode } from './tokens'

/**
 * Builds the MUI theme for a given mode from the semantic tokens.
 *
 * Component overrides read from the token set rather than hardcoded hex, so a
 * new mode only requires a new token entry.
 */
export function createAppTheme(mode: ThemeMode): Theme {
  const t = palettes[mode]
  const shadow = elevations[mode]

  return createTheme({
    palette: {
      mode,
      primary: {
        main: t.primary,
        dark: t.primaryHover,
        light: t.primaryActive,
        contrastText: t.primaryContrast,
      },
      secondary: { main: t.secondary },
      background: { default: t.background, paper: t.surface },
      text: { primary: t.textPrimary, secondary: t.textSecondary },
      divider: t.divider,
      error: { main: t.error },
      warning: { main: t.warning },
      success: { main: t.success },
      info: { main: t.info },
    },
    typography: {
      fontFamily:
        'Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif',
      h1: {
        fontSize: 'clamp(1.75rem, 6vw, 2.5rem)',
        fontWeight: 750,
        lineHeight: 1.15,
        letterSpacing: '-0.035em',
      },
      h2: { fontSize: '1.5rem', fontWeight: 730, lineHeight: 1.25, letterSpacing: '-0.025em' },
      h3: { fontSize: '1.125rem', fontWeight: 700 },
      button: { fontWeight: 700, textTransform: 'none', letterSpacing: '-0.01em' },
      body1: { lineHeight: 1.6 },
    },
    shape: { borderRadius: radii.lg },
    spacing: 8,
    shadows: [
      shadow[0],
      shadow[1],
      shadow[2],
      shadow[3],
      ...Array(21).fill(shadow[4]),
    ] as Theme['shadows'],
    components: {
      MuiCssBaseline: {
        styleOverrides: {
          // Tells the browser to render native controls/scrollbars for this mode.
          ':root': { colorScheme: mode },
          body: {
            backgroundImage: `radial-gradient(circle at 90% -10%, ${t.ambient}, transparent 32%)`,
          },
          // Honour the OS reduced-motion preference globally.
          '@media (prefers-reduced-motion: reduce)': {
            '*, *::before, *::after': {
              animationDuration: '0.01ms !important',
              animationIterationCount: '1 !important',
              transitionDuration: '0.01ms !important',
              scrollBehavior: 'auto !important',
            },
          },
          '::selection': {
            color: mode === 'light' ? '#10275f' : t.textPrimary,
            background: mode === 'light' ? '#c9d7ff' : 'rgba(126, 166, 255, 0.35)',
          },
        },
      },
      MuiButton: {
        defaultProps: { disableElevation: true },
        styleOverrides: {
          root: {
            minHeight: 44,
            borderRadius: radii.md,
            paddingInline: 20,
            transition: `transform ${motion.fast}ms ease, background-color ${motion.fast}ms ease`,
            '&:active': { transform: 'translateY(1px)' },
            '&:focus-visible': {
              outline: `3px solid ${t.primarySoft}`,
              outlineOffset: 2,
            },
          },
        },
      },
      MuiTextField: { defaultProps: { variant: 'outlined' } },
      MuiOutlinedInput: {
        styleOverrides: {
          root: {
            borderRadius: radii.md,
            backgroundColor: t.surface,
            '&.Mui-focused': { boxShadow: `0 0 0 3px ${t.primarySoft}` },
          },
        },
      },
      MuiCard: {
        styleOverrides: {
          root: {
            border: `1px solid ${t.divider}`,
            boxShadow: shadow[3],
            backgroundImage: 'none',
          },
        },
      },
      MuiPaper: {
        styleOverrides: {
          rounded: { borderRadius: radii.xl },
          // MUI's default dark-mode paper gradient fights our surface tokens.
          root: { backgroundImage: 'none' },
        },
      },
      MuiIconButton: {
        styleOverrides: {
          root: {
            '&:focus-visible': { outline: `3px solid ${t.primarySoft}` },
          },
        },
      },
      MuiBackdrop: {
        styleOverrides: { root: { backgroundColor: t.scrim } },
      },
      MuiDialog: {
        styleOverrides: {
          paper: {
            '@media (max-width:600px)': {
              margin: 16,
              width: 'calc(100% - 32px)',
              maxHeight: 'calc(100% - 32px)',
            },
          },
        },
      },
      MuiDialogActions: {
        styleOverrides: {
          root: {
            gap: 8,
            flexWrap: 'wrap',
            padding: 16,
            '& > :not(style) ~ :not(style)': { marginLeft: 0 },
          },
        },
      },
      MuiTooltip: {
        styleOverrides: {
          tooltip: { fontSize: '0.8125rem', padding: '6px 10px' },
        },
      },
    },
  })
}
