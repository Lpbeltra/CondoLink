import { Chip } from '@mui/material'
import { priorityPresentation } from '../presentation'
import type { RequestPriority } from '../types'

export function RequestPriorityChip({ priority }: { priority: RequestPriority }) {
  const item = priorityPresentation[priority]
  return <Chip label={item.label} color={item.color} size="small" variant={priority === 'Normal' ? 'outlined' : 'filled'} />
}
