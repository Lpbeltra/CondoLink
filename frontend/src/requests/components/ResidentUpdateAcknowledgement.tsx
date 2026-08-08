import { useState } from 'react'
import { Alert, Button } from '@mui/material'
import { acknowledgeResidentUpdate } from '../api'
import { getRequestError } from '../presentation'

export function ResidentUpdateAcknowledgement({ requestId, visible, onAcknowledged }: {
  requestId: string
  visible: boolean
  onAcknowledged?: () => void
}) {
  const [hidden, setHidden] = useState(false)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  if (!visible || hidden) return null

  const acknowledge = async () => {
    if (loading) return
    setLoading(true); setError('')
    try {
      await acknowledgeResidentUpdate(requestId)
      setHidden(true)
      onAcknowledged?.()
    } catch (requestError) {
      setError(getRequestError(requestError,
        'Não foi possível marcar a atualização como ciente.'))
    } finally { setLoading(false) }
  }

  return <>
    <Alert severity="info" sx={{ mb: 2 }} action={
      <Button color="inherit" disabled={loading} onClick={() => void acknowledge()}>
        {loading ? 'Marcando…' : '✓ Marcar como ciente'}
      </Button>
    }>Atualizado pelo morador</Alert>
    {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
  </>
}
