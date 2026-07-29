import { Box, Typography } from '@mui/material'

interface BrandProps { compact?: boolean }

export function Brand({ compact = false }: BrandProps) {
  return (
    <Box
      display="flex"
      alignItems="center"
      gap={1.25}
      role="img"
      aria-label="Comvy"
    >
      <Box sx={{ width: 38, height: 38, borderRadius: '12px', display: 'grid', placeItems: 'center', color: 'white', bgcolor: 'primary.main', boxShadow: '0 8px 20px rgba(31,94,255,.25)' }}>
        <Box component="svg" viewBox="0 0 40 40" width={30} height={30} aria-hidden>
          <path fill="currentColor" d="M20 6C11.7 6 5 11.8 5 19c0 4 2.1 7.7 5.7 10.1L9.3 35l6.3-3.1c1.4.4 2.9.6 4.4.6 8.3 0 15-5.8 15-13.3S28.3 6 20 6Zm7.2 17.2a9.5 9.5 0 0 1-7.1 3.1c-4.7 0-8.4-3.1-8.4-7.1s3.7-7.1 8.4-7.1c2.8 0 5.4 1.1 7 3l-3 2.3a5.2 5.2 0 0 0-4-1.7c-2.5 0-4.5 1.5-4.5 3.5s2 3.5 4.5 3.5c1.6 0 3-.6 4-1.7l3.1 2.2Z"/>
        </Box>
      </Box>
      {!compact && <Typography fontSize="1.15rem" fontWeight={800} letterSpacing="-.03em">Comvy</Typography>}
    </Box>
  )
}
