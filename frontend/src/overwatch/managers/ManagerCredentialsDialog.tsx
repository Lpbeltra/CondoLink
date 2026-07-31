import ContentCopyRoundedIcon from '@mui/icons-material/ContentCopyRounded'
import {
  Alert, Button, Card, CardContent, Dialog, DialogActions,
  DialogContent, DialogTitle, Stack, Typography,
} from '@mui/material'
import { managerCredentialsText } from './presentation'
import type { CreatedManager } from './types'

interface Props {
  manager: CreatedManager | null
  onClose: () => void
  onCopied: (message: string) => void
}

export function ManagerCredentialsDialog({ manager, onClose, onCopied }: Props) {
  const copy = async (value: string, message: string) => {
    try {
      await navigator.clipboard.writeText(value)
      onCopied(message)
    } catch {
      onCopied('Não foi possível copiar. Selecione o conteúdo manualmente.')
    }
  }
  return (
    <Dialog
      open={Boolean(manager)}
      onClose={() => undefined}
      disableEscapeKeyDown
      fullWidth
      maxWidth="sm"
    >
      <DialogTitle>Síndico criado com sucesso</DialogTitle>
      <DialogContent>
        <Alert severity="warning">
          A senha temporária será exibida somente neste momento. Compartilhe-a de forma segura.
        </Alert>
        {manager && (
          <Card variant="outlined" sx={{ mt: 2 }}>
            <CardContent>
              <Typography fontWeight={800}>{manager.fullName}</Typography>
              <Typography mt={1}>E-mail: {manager.email}</Typography>
              <Typography sx={{ fontFamily: 'monospace', mt: 1 }}>
                Senha temporária: {manager.temporaryPassword}
              </Typography>
              <Stack direction={{ xs: 'column', sm: 'row' }} gap={1} mt={2}>
                <Button startIcon={<ContentCopyRoundedIcon />}
                  onClick={() => void copy(manager.email, 'E-mail copiado.')}>
                  Copiar e-mail
                </Button>
                <Button startIcon={<ContentCopyRoundedIcon />}
                  onClick={() => void copy(manager.temporaryPassword, 'Senha copiada.')}>
                  Copiar senha
                </Button>
              </Stack>
            </CardContent>
          </Card>
        )}
      </DialogContent>
      <DialogActions>
        {manager && (
          <Button startIcon={<ContentCopyRoundedIcon />}
            onClick={() => void copy(managerCredentialsText(manager), 'Mensagem copiada.')}>
            Copiar mensagem para WhatsApp
          </Button>
        )}
        <Button variant="contained" onClick={onClose}>Concluir</Button>
      </DialogActions>
    </Dialog>
  )
}
