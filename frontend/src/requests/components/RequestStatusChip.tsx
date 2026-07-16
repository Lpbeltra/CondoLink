import { Chip } from '@mui/material'
import { statusPresentation } from '../presentation'
import type { RequestStatus } from '../types'

export function RequestStatusChip({ status }: { status: RequestStatus }) {
  const item = statusPresentation[status]
  return <Chip label={item.label} color={item.color} size="small" sx={{ fontWeight: 700 }} />
}
