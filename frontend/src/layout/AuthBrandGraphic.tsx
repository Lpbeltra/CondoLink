import { alpha, useTheme } from '@mui/material/styles'

export function AuthBrandGraphic() {
  const theme = useTheme()
  const isDark = theme.palette.mode === 'dark'
  const primary = theme.palette.primary.main
  const massPrimary = alpha(primary, isDark ? 0.24 : 0.22)
  const massSecondary = alpha(primary, isDark ? 0.13 : 0.12)
  const orbit = alpha(primary, isDark ? 0.58 : 0.48)
  const accent = alpha(primary, isDark ? 0.9 : 0.72)
  const cShape = "M593 171 A260 260 0 1 0 593 469 L511 422 A160 160 0 1 1 511 218 Z"

  return (
    <svg
      aria-hidden="true"
      data-testid="auth-brand-graphic"
      focusable="false"
      viewBox="0 0 620 620"
      width="100%"
      height="100%"
    >
      <path d={cShape} fill={massPrimary} />
      <path d="M311 480 L244 595 L390 522 Z" fill={massPrimary} />
      <path d="M574 180 A246 246 0 0 0 508 119" fill="none" stroke={massSecondary} strokeWidth="3" />
      <path
        d="M18 289 C15 89 172 -24 397 -44"
        fill="none"
        stroke={orbit}
        strokeWidth="1.25"
      />
      <path
        d="M167 495 C99 256 224 27 493 -11"
        fill="none"
        stroke={orbit}
        strokeWidth="1"
      />
      <path
        d="M313 585 C460 649 599 571 652 462"
        fill="none"
        stroke={orbit}
        strokeWidth="1"
      />
      <circle cx="565" cy="157" r="7" fill={accent} />
      <circle cx="602" cy="316" r="2.25" fill={orbit} />
      <circle cx="602" cy="332" r="2.25" fill={orbit} />
      <circle cx="602" cy="348" r="2.25" fill={orbit} />
      <circle cx="602" cy="364" r="2.25" fill={orbit} />
    </svg>
  )
}
