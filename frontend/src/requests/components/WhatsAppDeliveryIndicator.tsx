import CheckRoundedIcon from '@mui/icons-material/CheckRounded'
import DoneAllRoundedIcon from '@mui/icons-material/DoneAllRounded'
import ScheduleRoundedIcon from '@mui/icons-material/ScheduleRounded'
import ErrorOutlineRoundedIcon from '@mui/icons-material/ErrorOutlineRounded'
import { Box, Tooltip } from '@mui/material'
import type { WhatsAppDelivery } from '../types'

const states = {
  Pending: { label: 'Enviando pelo WhatsApp', Icon: ScheduleRoundedIcon },
  Processing: { label: 'Enviando pelo WhatsApp', Icon: ScheduleRoundedIcon },
  Sent: { label: 'Enviado pelo WhatsApp', Icon: CheckRoundedIcon },
  Delivered: { label: 'Entregue pelo WhatsApp', Icon: DoneAllRoundedIcon },
  Read: { label: 'Lido no WhatsApp', Icon: DoneAllRoundedIcon },
  Failed: { label: 'Falha no envio pelo WhatsApp', Icon: ErrorOutlineRoundedIcon },
  PermanentlyFailed: { label: 'Falha no envio pelo WhatsApp', Icon: ErrorOutlineRoundedIcon },
  Cancelled: { label: 'Envio pelo WhatsApp cancelado', Icon: ErrorOutlineRoundedIcon },
  Skipped: { label: 'Envio pelo WhatsApp não realizado', Icon: ErrorOutlineRoundedIcon },
}

export function WhatsAppDeliveryIndicator({ delivery }: { delivery?: WhatsAppDelivery | null }) {
  if (!delivery || !Object.hasOwn(states, delivery.status)) return null
  const { label, Icon } = states[delivery.status]
  const failed = delivery.status === 'Failed' || delivery.status === 'PermanentlyFailed'
  return <Tooltip title={label} enterTouchDelay={0}>
    <Box component="span" role="img" aria-label={label} tabIndex={0}
      sx={{ display: 'inline-flex', verticalAlign: 'middle', ml: .5,
        color: failed ? 'error.main' : delivery.status === 'Read' ? 'primary.main' : 'text.secondary' }}>
      <Icon sx={{ fontSize: 15 }} />
    </Box>
  </Tooltip>
}
