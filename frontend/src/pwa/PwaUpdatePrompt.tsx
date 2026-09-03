import { Alert, Button, Snackbar } from '@mui/material'
import { useEffect, useState } from 'react'
import { applyPwaUpdate, subscribeToPwaUpdate } from './pwaUpdate'

export function PwaUpdatePrompt() {
  const [open, setOpen] = useState(false)
  const [updating, setUpdating] = useState(false)
  useEffect(() => subscribeToPwaUpdate(() => setOpen(true)), [])
  const update = async () => {
    if (updating) return
    setUpdating(true)
    try { await applyPwaUpdate() } finally { setUpdating(false); setOpen(false) }
  }
  return <Snackbar role="status" open={open} onClose={() => setOpen(false)} anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}>
    <Alert severity="info" onClose={() => setOpen(false)} action={<Button color="inherit" size="small" disabled={updating} onClick={() => void update()}>Atualizar agora</Button>}>Nova versão do Comvy disponível.</Alert>
  </Snackbar>
}
