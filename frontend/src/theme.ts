import { alpha, createTheme } from '@mui/material/styles'

const primary = '#1f5eff'

export const theme = createTheme({
  palette: {
    mode: 'light',
    primary: { main: primary, dark: '#1747c7', light: '#5f88ff', contrastText: '#ffffff' },
    secondary: { main: '#7259d9' },
    background: { default: '#f6f8fc', paper: '#ffffff' },
    text: { primary: '#172033', secondary: '#65708a' },
    divider: '#e6eaf2',
    error: { main: '#d13c4b' },
  },
  typography: {
    fontFamily: 'Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif',
    h1: { fontSize: 'clamp(1.75rem, 6vw, 2.5rem)', fontWeight: 750, lineHeight: 1.15, letterSpacing: '-0.035em' },
    h2: { fontSize: '1.5rem', fontWeight: 730, lineHeight: 1.25, letterSpacing: '-0.025em' },
    h3: { fontSize: '1.125rem', fontWeight: 700 },
    button: { fontWeight: 700, textTransform: 'none', letterSpacing: '-0.01em' },
    body1: { lineHeight: 1.6 },
  },
  shape: { borderRadius: 14 },
  spacing: 8,
  shadows: [
    'none', '0 1px 2px rgba(23,32,51,.04)', '0 4px 14px rgba(23,32,51,.06)', '0 8px 24px rgba(23,32,51,.08)',
    ...Array(21).fill('0 12px 36px rgba(23,32,51,.10)'),
  ] as typeof createTheme extends (...args: never[]) => infer T ? T extends { shadows: infer S } ? S : never : never,
  components: {
    MuiCssBaseline: { styleOverrides: { body: { backgroundImage: 'radial-gradient(circle at 90% -10%, rgba(31,94,255,.08), transparent 32%)' } } },
    MuiButton: { defaultProps: { disableElevation: true }, styleOverrides: { root: { minHeight: 44, borderRadius: 12, paddingInline: 20, transition: 'transform 150ms ease, background-color 150ms ease', '&:active': { transform: 'translateY(1px)' }, '&:focus-visible': { outline: `3px solid ${alpha(primary, .24)}`, outlineOffset: 2 } } } },
    MuiTextField: { defaultProps: { variant: 'outlined' } },
    MuiOutlinedInput: { styleOverrides: { root: { borderRadius: 12, backgroundColor: '#fff', '&.Mui-focused': { boxShadow: `0 0 0 3px ${alpha(primary, .11)}` } } } },
    MuiCard: { styleOverrides: { root: { border: '1px solid #e6eaf2', boxShadow: '0 16px 48px rgba(23,32,51,.09)' } } },
    MuiPaper: { styleOverrides: { rounded: { borderRadius: 18 } } },
    MuiIconButton: { styleOverrides: { root: { '&:focus-visible': { outline: `3px solid ${alpha(primary, .22)}` } } } },
  },
})
