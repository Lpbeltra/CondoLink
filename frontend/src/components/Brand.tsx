import { Box, Typography } from '@mui/material'
import ApartmentRoundedIcon from '@mui/icons-material/ApartmentRounded'

interface BrandProps { compact?: boolean }

export function Brand({ compact = false }: BrandProps) {
  return (
    <Box display="flex" alignItems="center" gap={1.25} aria-label="CondoLink">
      <Box sx={{ width: 38, height: 38, borderRadius: '12px', display: 'grid', placeItems: 'center', color: 'white', bgcolor: 'primary.main', boxShadow: '0 8px 20px rgba(31,94,255,.25)' }}>
        <ApartmentRoundedIcon fontSize="small" />
      </Box>
      {!compact && <Typography fontSize="1.15rem" fontWeight={800} letterSpacing="-.03em">CondoLink</Typography>}
    </Box>
  )
}
