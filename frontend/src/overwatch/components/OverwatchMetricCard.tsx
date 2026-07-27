import type { ReactNode } from 'react'
import { Box, Card, CardContent, Skeleton, Typography, alpha } from '@mui/material'

interface OverwatchMetricCardProps {
  label: string
  value: string | number
  icon: ReactNode
  isLoading?: boolean
}

export function OverwatchMetricCard({
  label, value, icon, isLoading = false,
}: OverwatchMetricCardProps) {
  return (
    <Card elevation={0} sx={{ height: '100%' }}>
      <CardContent sx={{ p: 2.5, '&:last-child': { pb: 2.5 } }}>
        <Box display="flex" alignItems="center" justifyContent="space-between" gap={2}>
          <Box>
            <Typography color="text.secondary" fontSize=".85rem" fontWeight={700}>
              {label}
            </Typography>
            {isLoading
              ? <Skeleton width={52} height={38} aria-label={`Carregando ${label}`} />
              : <Typography variant="h2" mt={.5}>{value}</Typography>}
          </Box>
          <Box
            display="grid"
            width={44}
            height={44}
            borderRadius={2.5}
            color="primary.main"
            sx={(theme) => ({
              placeItems: 'center',
              bgcolor: alpha(theme.palette.primary.main, 0.09),
            })}
          >
            {icon}
          </Box>
        </Box>
      </CardContent>
    </Card>
  )
}
