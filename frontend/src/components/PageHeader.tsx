import { Box, Stack, Typography } from '@mui/material'
import type { ReactNode } from 'react'

export function PageHeader({ title, description, eyebrow, actions }: { title: string; description?: ReactNode; eyebrow?: ReactNode; actions?: ReactNode }) {
  return <Stack direction={{ xs: 'column', sm: 'row' }} justifyContent="space-between" alignItems={{ xs: 'stretch', sm: 'flex-start' }} gap={1.5} mb={{ xs: 2, md: 2.5 }}>
    <Box minWidth={0}>
      {eyebrow && <Typography variant="overline" color="text.secondary" sx={{ fontWeight: 800, letterSpacing: '.1em' }}>{eyebrow}</Typography>}
      <Typography variant="h1">{title}</Typography>
      {description && <Typography color="text.secondary" mt={.35}>{description}</Typography>}
    </Box>
    {actions && <Stack direction="row" gap={1} flexWrap="wrap" justifyContent={{ xs: 'flex-start', sm: 'flex-end' }}>{actions}</Stack>}
  </Stack>
}
