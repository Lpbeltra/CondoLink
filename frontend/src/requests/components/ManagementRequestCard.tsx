import ChevronRightRoundedIcon from '@mui/icons-material/ChevronRightRounded'
import { Alert, Box, Card, CardActionArea, CardContent, Stack, Typography } from '@mui/material'
import { useNavigate } from 'react-router-dom'
import { formatRelativeDate, formatRequestProtocol } from '../presentation'
import type { ManagementRequestItem } from '../types'
import { RequestPriorityChip } from './RequestPriorityChip'
import { RequestStatusChip } from './RequestStatusChip'

export function ManagementRequestCard({ request }: { request: ManagementRequestItem }) {
  const navigate = useNavigate()
  const unit = request.targetUnit && `${request.targetUnit.block ? `Bloco ${request.targetUnit.block} · ` : ''}${request.targetUnit.identifier}`
  return <Card elevation={0} sx={{ boxShadow: 'none' }}><CardActionArea onClick={() => navigate(`/management/requests/${request.id}`)}>
    <CardContent sx={{ p: { xs: 2.25, sm: 2.75 } }}>
      <Box display="flex" gap={2} alignItems="flex-start">
        <Box flex={1} minWidth={0}><Typography color="primary.main" fontSize=".76rem" fontWeight={800}>Atendimento #{formatRequestProtocol(request.id, request.protocol)}</Typography><Typography variant="h3" mt={.35}>{request.title}</Typography><Typography color="text.secondary" fontSize=".84rem" mt={.5}>{request.author.fullName} · {request.category.name}</Typography></Box>
        <ChevronRightRoundedIcon color="action" />
      </Box>
      <Stack direction="row" flexWrap="wrap" gap={1} mt={2}><RequestStatusChip status={request.status} /><RequestPriorityChip priority={request.priority} /></Stack>
      {request.hasUnreadResidentReply && <Alert severity="warning" sx={{ mt: 1.5, py: 0 }}>Morador respondeu</Alert>}
      {request.hasUnreadResidentUpdate && <Alert severity="info" sx={{ mt: 1.5, py: 0 }}>Atualizado pelo morador</Alert>}
      <Typography color="primary.main" fontSize=".8rem" fontWeight={750} mt={1.5}>{request.condominiumName}</Typography>
      <Box display="flex" justifyContent="space-between" gap={2} mt={2}><Typography color="text.secondary" fontSize=".8rem" noWrap>{unit || 'Sem unidade relacionada'}</Typography><Typography color="text.secondary" fontSize=".8rem" flexShrink={0}>{formatRelativeDate(request.updatedAt)}</Typography></Box>
    </CardContent>
  </CardActionArea></Card>
}
