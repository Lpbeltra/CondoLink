import WhatsAppIcon from '@mui/icons-material/WhatsApp'
import { Button, type ButtonProps } from '@mui/material'
import { whatsAppUrl } from './whatsApp'

type Props = Omit<ButtonProps<'a'>, 'href' | 'component'> & { phone: string | null | undefined; message?: string }

/** Native external link: keep the SPA document, history and condominium context intact. */
export function WhatsAppButton({ phone, message, children = 'Abrir WhatsApp', disabled, ...props }: Props) {
  const href = whatsAppUrl(phone, message)
  return href
    ? <Button href={href} target="_blank" rel="noopener noreferrer" startIcon={<WhatsAppIcon />} disabled={disabled} {...props}>{children}</Button>
    : <Button component="a" aria-disabled="true" tabIndex={-1} disabled startIcon={<WhatsAppIcon />} {...props}>{children}</Button>
}
