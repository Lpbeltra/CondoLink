import { Box, Button, Typography } from '@mui/material'
import InboxRoundedIcon from '@mui/icons-material/InboxRounded'
import type { ReactNode } from 'react'

interface EmptyStateProps { title: string; description: string; action?: ReactNode; actionLabel?: string; onAction?: () => void }

export function EmptyState({ title, description, action, actionLabel, onAction }: EmptyStateProps) {
  return (
    <Box textAlign="center" p={4} border="1px dashed" borderColor="divider" borderRadius={3}>
      <InboxRoundedIcon color="primary" sx={{ fontSize: 36 }} />
      <Typography variant="h3" mt={1}>{title}</Typography>
      <Typography color="text.secondary" mt={.5}>{description}</Typography>
      {(action || actionLabel) && <Box mt={2.5}>{action ?? <Button onClick={onAction}>{actionLabel}</Button>}</Box>}
    </Box>
  )
}
