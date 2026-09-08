import { useState } from 'react'
import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, Stack, TextField, Typography } from '@mui/material'
import { prepareProviderContactMessage } from '../api'

export function normalizeWhatsAppPhone(phone: string) { const digits = phone.replace(/\D/g, ''); return digits.length === 10 || digits.length === 11 ? `55${digits}` : digits }
export function hasWhatsAppPhone(phone: string) { const normalized = normalizeWhatsAppPhone(phone); return /^55\d{10,11}$/.test(normalized) }
export function providerWhatsAppUrl(phone: string, message: string) { return `https://wa.me/${normalizeWhatsAppPhone(phone)}?text=${encodeURIComponent(message)}` }
export function OpenProviderWhatsAppButton({ phone }: { phone: string }) { return <Button size="small" onClick={() => window.open(providerWhatsAppUrl(phone, ''), '_blank', 'noopener,noreferrer')}>Abrir WhatsApp</Button> }

export function ProviderWhatsAppDialog({ requestId, provider, onClose }: { requestId: string; provider: { name: string; specialty: string; companyName?: string | null; phone: string }; onClose: () => void }) {
  const [message, setMessage] = useState(''), [loading, setLoading] = useState(false), [error, setError] = useState('')
  const prepare = async () => { setLoading(true); setError(''); try { setMessage((await prepareProviderContactMessage(requestId)).message) } catch { setError('Não foi possível preparar a mensagem. Você pode escrevê-la manualmente.') } finally { setLoading(false) } }
  const openWhatsApp = () => { window.open(providerWhatsAppUrl(provider.phone, message), '_blank', 'noopener,noreferrer'); onClose() }
  return <Dialog open onClose={onClose} fullWidth maxWidth="sm"><DialogTitle>Contatar pelo WhatsApp</DialogTitle><DialogContent><Stack gap={1.5} mt={1}><Typography><strong>Prestador:</strong> {provider.name} — {provider.specialty}{provider.companyName ? ` · ${provider.companyName}` : ''}</Typography><Typography color="text.secondary"><strong>Telefone:</strong> {provider.phone}</Typography>{error && <Alert severity="warning">{error}</Alert>}<TextField label="Mensagem" multiline minRows={6} value={message} onChange={e => setMessage(e.target.value)} placeholder="Escreva uma mensagem para o prestador" fullWidth /></Stack></DialogContent><DialogActions><Button onClick={onClose}>Cancelar</Button><Button onClick={() => void prepare()} disabled={loading}>{loading ? 'Preparando…' : 'Preparar com IA'}</Button><Button variant="contained" onClick={openWhatsApp} disabled={!message.trim()}>Abrir WhatsApp</Button></DialogActions></Dialog>
}
