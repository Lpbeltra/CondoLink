import ChevronRightRoundedIcon from '@mui/icons-material/ChevronRightRounded'
import { Box, Card, CardActionArea, CardContent, Stack, Typography } from '@mui/material'
import { useNavigate } from 'react-router-dom'
import { formatRelativeDate } from '../presentation'
import type { RequestListItem } from '../types'
import { RequestPriorityChip } from './RequestPriorityChip'
import { RequestStatusChip } from './RequestStatusChip'

export function RequestCard({ request }: { request: RequestListItem }) {
  const navigate = useNavigate()
  const unit = request.targetUnit && `${request.targetUnit.block ? `Bloco ${request.targetUnit.block} · ` : ''}${request.targetUnit.identifier}`
  return (
    <Card elevation={0} sx={{ boxShadow: 'none', transition: 'border-color 150ms ease, transform 150ms ease', '&:hover': { borderColor: 'primary.light', transform: 'translateY(-1px)' } }}>
      <CardActionArea onClick={() => navigate(`/requests/${request.id}`)} sx={{ minHeight: 148 }}>
        <CardContent sx={{ p: { xs: 2.25, sm: 2.75 } }}>
          <Box display="flex" gap={1.5} alignItems="flex-start">
            <Box flex={1} minWidth={0}>
              <Typography color="text.secondary" fontSize=".8rem" fontWeight={700}>{request.category.name}</Typography>
              <Typography variant="h3" mt={.5}>{request.title}</Typography>
            </Box>
            <ChevronRightRoundedIcon color="action" />
          </Box>
          <Stack direction="row" flexWrap="wrap" gap={1} mt={2}><RequestStatusChip status={request.status} />{request.priority !== 'Normal' && <RequestPriorityChip priority={request.priority} />}</Stack>
          <Box display="flex" justifyContent="space-between" gap={2} mt={2}>
            <Typography color="text.secondary" fontSize=".8rem" noWrap>{unit || 'Sem unidade relacionada'}</Typography>
            <Typography color="text.secondary" fontSize=".8rem" flexShrink={0}>{formatRelativeDate(request.updatedAt)}</Typography>
          </Box>
        </CardContent>
      </CardActionArea>
    </Card>
  )
}
