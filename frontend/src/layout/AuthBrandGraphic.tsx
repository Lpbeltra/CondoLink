import { alpha, useTheme } from '@mui/material/styles'

export function AuthBrandGraphic() {
  const theme = useTheme()
  const isDark = theme.palette.mode === 'dark'
  const primary = theme.palette.primary.main
  const massPrimary = alpha(primary, isDark ? 0.19 : 0.2)
  const massSecondary = alpha(primary, isDark ? 0.12 : 0.13)
  const orbit = alpha(primary, isDark ? 0.48 : 0.42)
  const accent = alpha(primary, isDark ? 0.9 : 0.72)

  return (
    <svg
      aria-hidden="true"
      data-testid="auth-brand-graphic"
      focusable="false"
      viewBox="0 0 620 620"
      width="100%"
      height="100%"
    >
      <path d="M654 78 C503 6 324 35 242 161 L312 215 C369 131 477 108 571 150 Z" fill={massPrimary} />
      <path d="M228 211 C157 315 191 452 306 513 L352 432 C289 399 267 327 310 263 Z" fill={massSecondary} />
      <path d="M367 470 C470 496 584 448 643 360 L558 305 C513 370 439 396 375 372 Z" fill={massPrimary} />
      <path
        d="M278 441 L205 584 L351 507 Z"
        fill={massSecondary}
      />
      <path
        d="M-46 306 C22 91 188 -30 374 -37"
        fill="none"
        stroke={orbit}
        strokeWidth="1.25"
      />
      <path
        d="M250 631 C446 676 638 573 680 395"
        fill="none"
        stroke={orbit}
        strokeWidth="1"
      />
      <path
        d="M355 -43 C593 8 721 209 663 420"
        fill="none"
        stroke={orbit}
        strokeWidth="1"
      />
      <circle cx="508" cy="107" r="7" fill={accent} />
      <circle cx="570" cy="318" r="2.25" fill={orbit} />
      <circle cx="570" cy="334" r="2.25" fill={orbit} />
      <circle cx="570" cy="350" r="2.25" fill={orbit} />
      <circle cx="570" cy="366" r="2.25" fill={orbit} />
    </svg>
  )
}
