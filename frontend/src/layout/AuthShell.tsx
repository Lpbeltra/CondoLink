import type { PropsWithChildren } from 'react'
import { Box } from '@mui/material'
import { Brand } from '../components/Brand'
import { ThemeModeToggle } from '../theme/ThemeModeToggle'
import { AuthBrandGraphic } from './AuthBrandGraphic'

export function AuthShell({ children }: PropsWithChildren) {
  return (
    <Box
      component="main"
      sx={{
        minHeight: '100dvh',
        display: 'flex',
        flexDirection: 'column',
        px: {
          xs: 'calc(20px + env(safe-area-inset-left))',
          sm: 'calc(32px + env(safe-area-inset-left))',
        },
        pr: {
          xs: 'calc(20px + env(safe-area-inset-right))',
          sm: 'calc(32px + env(safe-area-inset-right))',
        },
        pt: 'calc(20px + env(safe-area-inset-top))',
        pb: 'calc(20px + env(safe-area-inset-bottom))',
      }}
    >
      <Box
        component="header"
        sx={{
          width: '100%',
          maxWidth: 1180,
          mx: 'auto',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
        }}
      >
        <Brand />
        <ThemeModeToggle />
      </Box>
      <Box
        sx={{
          width: '100%',
          maxWidth: 1180,
          mx: 'auto',
          flex: 1,
          display: 'flex',
          position: 'relative',
          alignItems: { xs: 'flex-start', md: 'center' },
          pt: { xs: 7, md: 0 },
          pb: { xs: 5, md: 7 },
        }}
      >
        <Box
          sx={{
            width: '100%',
            maxWidth: 440,
            ml: { xs: 0, md: 'clamp(40px, 10vw, 152px)' },
          }}
        >
          <Box
            sx={{
              display: { xs: 'none', lg: 'block' },
              position: 'absolute',
              width: 'clamp(360px, 43vw, 560px)',
              height: 'clamp(360px, 43vw, 560px)',
              right: 'clamp(-56px, -3vw, -16px)',
              top: '50%',
              transform: 'translateY(-50%)',
              pointerEvents: 'none',
            }}
          >
            <AuthBrandGraphic />
          </Box>
          <Box sx={{ position: 'relative', zIndex: 1 }}>
            {children}
          </Box>
        </Box>
      </Box>
    </Box>
  )
}
