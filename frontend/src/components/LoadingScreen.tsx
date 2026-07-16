import { Box, CircularProgress, Typography } from '@mui/material'
import { Brand } from './Brand'

export function LoadingScreen() {
  return (
    <Box minHeight="100dvh" display="grid" sx={{ placeItems: 'center' }}>
      <Box textAlign="center">
        <Brand />
        <CircularProgress size={28} thickness={4} sx={{ mt: 4 }} />
        <Typography color="text.secondary" mt={1.5} fontSize=".875rem">Preparando seu espaço…</Typography>
      </Box>
    </Box>
  )
}
