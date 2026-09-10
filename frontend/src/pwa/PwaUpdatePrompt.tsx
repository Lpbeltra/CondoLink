import { Alert, Button, Snackbar } from '@mui/material'
import { useSyncExternalStore } from 'react'
import {
  applyPwaUpdate,
  getPwaUpdateState,
  subscribeToPwaUpdate,
} from './pwaUpdate'

export function PwaUpdatePrompt() {
  const { error, updateAvailable, updating } = useSyncExternalStore(
    subscribeToPwaUpdate,
    getPwaUpdateState,
  )

  return <Snackbar role="status" open={updateAvailable} anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}>
    <Alert
      severity={error ? 'error' : 'info'}
      action={
        <Button color="inherit" size="small" disabled={updating} onClick={() => void applyPwaUpdate()}>
          {updating ? 'Atualizando…' : 'Atualizar agora'}
        </Button>
      }
    >
      {error ?? 'Nova versão do Comvy disponível.'}
    </Alert>
  </Snackbar>
}
