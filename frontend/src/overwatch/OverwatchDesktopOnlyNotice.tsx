import { Box, Stack, Typography } from '@mui/material'

export function OverwatchDesktopOnlyNotice() {
  return (
    <Box
      component="main"
      sx={{
        minHeight: '100dvh',
        display: 'grid',
        placeItems: 'center',
        px: 2,
        py: 'calc(24px + env(safe-area-inset-top))',
      }}
    >
      <Stack spacing={1} textAlign="center" maxWidth={440}>
        <Typography variant="h2">Overwatch disponível no desktop</Typography>
        <Typography color="text.secondary">
          O painel administrativo global do Comvy está disponível na versão desktop.
        </Typography>
      </Stack>
    </Box>
  )
}
