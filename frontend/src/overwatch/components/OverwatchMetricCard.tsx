import type { ReactNode } from 'react'
import { Box, Card, CardContent, Skeleton, Typography } from '@mui/material'

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
            sx={{ placeItems: 'center' }}
            width={44}
            height={44}
            borderRadius={2.5}
            bgcolor="rgba(31,94,255,.09)"
            color="primary.main"
          >
            {icon}
          </Box>
        </Box>
      </CardContent>
    </Card>
  )
}
