import type { ReactNode } from 'react'
import ExpandMoreRoundedIcon from '@mui/icons-material/ExpandMoreRounded'
import { Accordion, AccordionDetails, AccordionSummary, Box, Chip, Typography } from '@mui/material'
import { formatDateTime } from '../presentation'
import type { OriginalReport } from '../types'

export function OriginalReportAccordion({ report, attachments }: {
  report: OriginalReport | null
  attachments?: ReactNode
}) {
  if (!report) return null
  return (
    <Accordion disableGutters elevation={0} sx={{ mt: 3, border: '1px solid', borderColor: 'divider', borderRadius: '12px !important', '&::before': { display: 'none' } }}>
      <AccordionSummary expandIcon={<ExpandMoreRoundedIcon />}>
        <Typography variant="h2">Relato original do morador</Typography>
      </AccordionSummary>
      <AccordionDetails>
        <Box display="flex" flexWrap="wrap" gap={1} mb={2}>
          <Chip label="Origem: WhatsApp" size="small" variant="outlined" />
          <Typography color="text.secondary" fontSize=".8rem" alignSelf="center">{formatDateTime(report.createdAt)}</Typography>
        </Box>
        {report.text && <Typography sx={{ whiteSpace: 'pre-wrap', overflowWrap: 'anywhere' }}>{report.text}</Typography>}
        {attachments}
      </AccordionDetails>
    </Accordion>
  )
}
