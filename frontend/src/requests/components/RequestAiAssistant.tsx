import { Box, Card, CardContent, Chip, Stack, Typography } from '@mui/material'
import type { RequestAiAnalysis } from '../types'

export function confidenceLabel(confidence: number) {
  if (confidence < 0.5) return 'Baixa'
  if (confidence < 0.8) return 'Média'
  return 'Alta'
}

export function canViewInternalRequestDetails(
  managementMode: boolean,
  isManager: boolean,
  requestCondominiumId: string,
  expectedCondominiumId?: string | null,
) {
  return managementMode
    || (isManager && requestCondominiumId === expectedCondominiumId)
}

export function RequestAiAssistant({ analysis }: { analysis: RequestAiAnalysis | null }) {
  if (!analysis) return null
  const hasDetails = analysis.suggestedCategory
    || analysis.confidence !== null
    || analysis.missingInformation.length > 0
  if (!hasDetails) return null

  return (
    <Card elevation={0} sx={{ mt: 3, border: '1px solid', borderColor: 'divider' }}>
      <CardContent sx={{ p: { xs: 2.5, sm: 3 } }}>
        <Typography variant="h2" mb={2}>Assistente Comvy</Typography>
        <Stack spacing={2}>
          {analysis.suggestedCategory && (
            <Box>
              <Typography color="text.secondary" fontSize=".8rem">Categoria sugerida pela IA</Typography>
              <Typography fontWeight={700}>{analysis.suggestedCategory}</Typography>
            </Box>
          )}
          {analysis.confidence !== null && (
            <Box>
              <Typography color="text.secondary" fontSize=".8rem" mb={.5}>Confiança da análise</Typography>
              <Chip size="small" variant="outlined" label={confidenceLabel(analysis.confidence)} />
            </Box>
          )}
          {analysis.missingInformation.length > 0 && (
            <Box>
              <Typography fontWeight={700} mb={.5}>Possíveis informações pendentes</Typography>
              <Box component="ul" sx={{ my: 0, pl: 2.5 }}>
                {analysis.missingInformation.map((item) => <li key={item}><Typography component="span">{item}</Typography></li>)}
              </Box>
            </Box>
          )}
        </Stack>
      </CardContent>
    </Card>
  )
}
