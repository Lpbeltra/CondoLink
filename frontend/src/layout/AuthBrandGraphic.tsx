import { alpha, useTheme } from '@mui/material/styles'

export function AuthBrandGraphic() {
  const theme = useTheme()
  const dark = theme.palette.mode === 'dark'
  const opacity = dark ? 0.2 : 0.16
  const orbit = alpha(theme.palette.primary.main, dark ? 0.58 : 0.48)
  const accent = alpha(theme.palette.primary.main, dark ? 0.9 : 0.72)

  return (
    <svg
      aria-hidden="true"
      data-testid="auth-brand-graphic"
      focusable="false"
      viewBox="0 0 620 620"
      width="100%"
      height="100%"
    >
      <image href="/comvy-symbol.svg" x="8" y="3" width="604" height="614" opacity={opacity} />
      <path d="M574 180 A246 246 0 0 0 508 119" fill="none" stroke={orbit} strokeWidth="3" />
      <path d="M18 289 C15 89 172 -24 397 -44" fill="none" stroke={orbit} strokeWidth="1.25" />
      <path d="M167 495 C99 256 224 27 493 -11" fill="none" stroke={orbit} strokeWidth="1" />
      <path d="M313 585 C460 649 599 571 652 462" fill="none" stroke={orbit} strokeWidth="1" />
      <circle cx="565" cy="157" r="7" fill={accent} />
      <circle cx="602" cy="316" r="2.25" fill={orbit} />
      <circle cx="602" cy="332" r="2.25" fill={orbit} />
      <circle cx="602" cy="348" r="2.25" fill={orbit} />
      <circle cx="602" cy="364" r="2.25" fill={orbit} />
    </svg>
  )
}
