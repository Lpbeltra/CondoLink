import { useState } from 'react'
import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, Stack, TextField, Typography } from '@mui/material'
import { prepareProviderContactMessage } from '../api'
import { WhatsAppButton } from '../../communication/WhatsAppButton'

export function OpenProviderWhatsAppButton({ phone }: { phone: string }) { return <WhatsAppButton size="small" phone={phone} /> }

export function ProviderWhatsAppDialog({ requestId, provider, onClose }: { requestId: string; provider: { name: string; specialty: string; companyName?: string | null; phone: string }; onClose: () => void }) {
  const [message, setMessage] = useState(''), [loading, setLoading] = useState(false), [error, setError] = useState('')
  const prepare = async () => { setLoading(true); setError(''); try { setMessage((await prepareProviderContactMessage(requestId)).message) } catch { setError('Não foi possível preparar a mensagem. Você pode escrevê-la manualmente.') } finally { setLoading(false) } }
  return <Dialog open onClose={onClose} fullWidth maxWidth="sm"><DialogTitle>Contatar pelo WhatsApp</DialogTitle><DialogContent><Stack gap={1.5} mt={1}><Typography><strong>Prestador:</strong> {provider.name} — {provider.specialty}{provider.companyName ? ` · ${provider.companyName}` : ''}</Typography><Typography color="text.secondary"><strong>Telefone:</strong> {provider.phone}</Typography>{error && <Alert severity="warning">{error}</Alert>}<TextField label="Mensagem" multiline minRows={6} value={message} onChange={e => setMessage(e.target.value)} placeholder="Escreva uma mensagem para o prestador" fullWidth /></Stack></DialogContent><DialogActions><Button onClick={onClose}>Cancelar</Button><Button onClick={() => void prepare()} disabled={loading}>{loading ? 'Preparando…' : 'Preparar com IA'}</Button><WhatsAppButton variant="contained" phone={provider.phone} message={message} onClick={onClose} disabled={!message.trim()} /></DialogActions></Dialog>
}
