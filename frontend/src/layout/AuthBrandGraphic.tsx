import { useTheme } from '@mui/material/styles'

export function AuthBrandGraphic() {
  const theme = useTheme()
  const isDark = theme.palette.mode === 'dark'
  const primary = theme.palette.primary.main
  const surface = theme.palette.background.paper
  const formOpacity = isDark ? 0.14 : 0.08
  const orbitOpacity = isDark ? 0.44 : 0.3

  return (
    <svg
      aria-hidden="true"
      data-testid="auth-brand-graphic"
      focusable="false"
      viewBox="0 0 620 620"
      width="100%"
      height="100%"
    >
      <path
        d="M452 132 A210 210 0 1 0 469 451"
        fill="none"
        stroke={primary}
        strokeWidth="80"
        strokeLinecap="butt"
        opacity={formOpacity}
      />
      <path
        d="M282 430 L246 518 L338 470 Z"
        fill={primary}
        opacity={formOpacity}
      />
      <path
        d="M130 305 A265 265 0 0 1 492 55"
        fill="none"
        stroke={primary}
        strokeWidth="1.25"
        opacity={orbitOpacity}
      />
      <path
        d="M208 500 A265 265 0 0 0 543 231"
        fill="none"
        stroke={primary}
        strokeWidth="1"
        opacity={orbitOpacity * 0.65}
      />
      <path
        d="M88 371 A305 305 0 0 1 257 84"
        fill="none"
        stroke={primary}
        strokeWidth="1"
        opacity={orbitOpacity * 0.55}
      />
      <circle cx="474" cy="142" r="7" fill={primary} opacity={isDark ? 0.85 : 0.55} />
      <circle cx="537" cy="352" r="2.25" fill={primary} opacity={orbitOpacity} />
      <circle cx="537" cy="368" r="2.25" fill={primary} opacity={orbitOpacity} />
      <circle cx="537" cy="384" r="2.25" fill={primary} opacity={orbitOpacity} />
      <circle cx="537" cy="400" r="2.25" fill={primary} opacity={orbitOpacity} />
      <circle cx="320" cy="544" r="1.75" fill={surface} opacity={isDark ? 0.7 : 0.9} />
    </svg>
  )
}
