import { Box, Typography } from '@mui/material'
import InboxRoundedIcon from '@mui/icons-material/InboxRounded'
import type { ReactNode } from 'react'

interface EmptyStateProps { title: string; description: string; action?: ReactNode }

export function EmptyState({ title, description, action }: EmptyStateProps) {
  return (
    <Box textAlign="center" p={4} border="1px dashed" borderColor="divider" borderRadius={3}>
      <InboxRoundedIcon color="primary" sx={{ fontSize: 36 }} />
      <Typography variant="h3" mt={1}>{title}</Typography>
      <Typography color="text.secondary" mt={.5}>{description}</Typography>
      {action && <Box mt={2.5}>{action}</Box>}
    </Box>
  )
}
