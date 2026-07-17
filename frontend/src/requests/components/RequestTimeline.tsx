import { Box, Stack, Typography } from '@mui/material'
import { formatDateTime, statusPresentation } from '../presentation'
import type { StatusHistoryItem } from '../types'
import { newestStatusHistoryFirst } from '../requestUpdates'

export function RequestTimeline({ history }: { history: StatusHistoryItem[] }) {
  const orderedHistory = newestStatusHistoryFirst(history)

  return (
    <Stack component="ol" spacing={0} sx={{ listStyle: 'none', p: 0, m: 0 }}>
      {orderedHistory.map((item, index) => (
        <Box component="li" key={item.id} display="grid" gridTemplateColumns="24px 1fr" gap={1.5}>
          <Box display="flex" flexDirection="column" alignItems="center">
            <Box width={10} height={10} borderRadius="50%" bgcolor="primary.main" mt={.75} />
            {index < orderedHistory.length - 1 && <Box width="2px" flex={1} minHeight={46} bgcolor="divider" />}
          </Box>
          <Box pb={index < orderedHistory.length - 1 ? 2.5 : 0}>
            <Typography fontWeight={700}>{item.previousStatus === null ? 'Solicitação aberta' : `Status alterado para ${statusPresentation[item.newStatus].label}`}</Typography>
            <Typography color="text.secondary" fontSize=".82rem">{item.changedByFullName} · {formatDateTime(item.createdAt)}</Typography>
            {item.reason && <Typography mt={.75}>{item.reason}</Typography>}
          </Box>
        </Box>
      ))}
    </Stack>
  )
}
