import ChevronRightRoundedIcon from '@mui/icons-material/ChevronRightRounded'
import { Alert, Box, ButtonBase, Stack, Typography } from '@mui/material'
import { useNavigate } from 'react-router-dom'
import { formatRelativeDate, formatRequestProtocol } from '../presentation'
import type { ManagementRequestItem } from '../types'
import { RequestPriorityChip } from './RequestPriorityChip'
import { RequestStatusChip } from './RequestStatusChip'

export function ManagementRequestCard({ request }: { request: ManagementRequestItem }) {
  const navigate = useNavigate()
  const unit = request.targetUnit && `${request.targetUnit.block ? `Bloco ${request.targetUnit.block} · ` : ''}${request.targetUnit.identifier}`
  return <Box component="article" sx={{ borderBottom: '1px solid', borderColor: 'divider', '&:last-child': { borderBottom: 0 } }}>
    <ButtonBase onClick={() => navigate(`/management/requests/${request.id}`)} sx={{ width: '100%', display: 'block', textAlign: 'left', px: { xs: 1.25, sm: 1.5 }, py: 1.5, borderRadius: 1, '&:hover': { bgcolor: 'action.hover' }, '&:focus-visible': { outline: 2, outlineColor: 'primary.main', outlineOffset: -2 } }}>
      <Box display="flex" gap={1.5} alignItems="flex-start">
        <Box flex={1} minWidth={0}>
          <Typography variant="h3" sx={{ fontSize: '1rem', overflowWrap: 'anywhere' }}>{request.title}</Typography>
          <Typography color="text.secondary" fontSize=".82rem" mt={.35} noWrap>{request.author.fullName} · {unit || 'Sem unidade relacionada'}</Typography>
          <Stack direction="row" flexWrap="wrap" gap={.75} mt={1}>{<RequestStatusChip status={request.status} />}<RequestPriorityChip priority={request.priority} /></Stack>
          {(request.hasUnreadResidentReply || request.hasUnreadResidentUpdate) && <Stack direction="row" gap={.75} mt={1} flexWrap="wrap">{request.hasUnreadResidentReply && <Alert severity="warning" sx={{ py: 0, px: 1, '& .MuiAlert-icon': { mr: .5 } }}>Morador respondeu</Alert>}{request.hasUnreadResidentUpdate && <Alert severity="info" sx={{ py: 0, px: 1, '& .MuiAlert-icon': { mr: .5 } }}>Atualizado pelo morador</Alert>}</Stack>}
          <Stack direction="row" gap={1.5} mt={1} flexWrap="wrap"><Typography color="text.secondary" fontSize=".74rem">#{formatRequestProtocol(request.id, request.protocol)}</Typography><Typography color="text.secondary" fontSize=".74rem">{request.category.name}</Typography><Typography color="text.secondary" fontSize=".74rem">{formatRelativeDate(request.updatedAt)}</Typography>{request.condominiumName && <Typography color="text.secondary" fontSize=".74rem">{request.condominiumName}</Typography>}</Stack>
        </Box>
        <ChevronRightRoundedIcon color="action" sx={{ mt: .25, flexShrink: 0 }} />
      </Box>
    </ButtonBase>
  </Box>
}
