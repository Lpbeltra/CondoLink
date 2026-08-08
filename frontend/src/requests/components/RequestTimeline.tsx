import { Box, Stack, Typography } from '@mui/material'
import { formatDateTime, statusPresentation } from '../presentation'
import type { RequestMessage, StatusHistoryItem } from '../types'
import { newestStatusHistoryFirst } from '../requestUpdates'

export function RequestTimeline({ history, messages = [] }: { history: StatusHistoryItem[]; messages?: RequestMessage[] }) {
  const orderedHistory = [
    ...newestStatusHistoryFirst(history).map(item => ({ kind: 'status' as const, item, createdAt: item.createdAt, id: item.id })),
    ...messages.filter(message => message.channel === 'WhatsAppResidentUpdate')
      .map(item => ({ kind: 'resident-update' as const, item, createdAt: item.createdAt, id: item.id })),
  ].sort((left, right) => right.createdAt.localeCompare(left.createdAt) || right.id.localeCompare(left.id))

  return (
    <Stack component="ol" spacing={0} sx={{ listStyle: 'none', p: 0, m: 0 }}>
      {orderedHistory.map((entry, index) => (
        <Box component="li" key={`${entry.kind}-${entry.id}`} display="grid" gridTemplateColumns="24px 1fr" gap={1.5}>
          <Box display="flex" flexDirection="column" alignItems="center">
            <Box width={10} height={10} borderRadius="50%" bgcolor="primary.main" mt={.75} />
            {index < orderedHistory.length - 1 && <Box width="2px" flex={1} minHeight={46} bgcolor="divider" />}
          </Box>
          <Box pb={index < orderedHistory.length - 1 ? 2.5 : 0}>
            {entry.kind === 'status' ? <>
              <Typography fontWeight={700}>{entry.item.previousStatus === null ? 'Solicitação aberta' : `Status alterado para ${statusPresentation[entry.item.newStatus].label}`}</Typography>
              <Typography color="text.secondary" fontSize=".82rem">{entry.item.changedByFullName} · {formatDateTime(entry.item.createdAt)}</Typography>
              {entry.item.reason && <Typography mt={.75}>{entry.item.reason}</Typography>}
            </> : <>
              <Typography fontWeight={700}>Atualização do morador</Typography>
              <Typography color="text.secondary" fontSize=".82rem">{entry.item.author.fullName} · {formatDateTime(entry.item.createdAt)}</Typography>
              <Typography mt={.75} sx={{ whiteSpace: 'pre-wrap', overflowWrap: 'anywhere' }}>{entry.item.content}</Typography>
            </>}
          </Box>
        </Box>
      ))}
    </Stack>
  )
}
