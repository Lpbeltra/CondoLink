import { useEffect, useState } from 'react'
import CheckCircleRoundedIcon from '@mui/icons-material/CheckCircleRounded'
import PhoneAndroidRoundedIcon from '@mui/icons-material/PhoneAndroidRounded'
import {
  CircularProgress, ListItemIcon, MenuItem,
} from '@mui/material'
import { useNavigate } from 'react-router-dom'
import { getPhoneVerificationStatus, type PhoneVerificationStatus } from './api'

interface Props {
  closeMenu: () => void
  openDialog: () => void
}

export function PhoneVerificationMenuItem({ closeMenu, openDialog }: Props) {
  const navigate = useNavigate()
  const [status, setStatus] = useState<PhoneVerificationStatus | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    void getPhoneVerificationStatus()
      .then(setStatus)
      .catch(() => setStatus(null))
      .finally(() => setLoading(false))
  }, [])

  useEffect(() => {
    const update = (event: Event) =>
      setStatus((event as CustomEvent<PhoneVerificationStatus>).detail)
    window.addEventListener('condolink:phone-verification-updated', update)
    return () =>
      window.removeEventListener('condolink:phone-verification-updated', update)
  }, [])

  const open = () => {
    closeMenu()
    if (!status?.maskedPhoneNumber) {
      navigate('/more')
      return
    }
    if (!status.confirmed) openDialog()
  }

  const label = loading
    ? 'Carregando WhatsApp'
    : !status?.maskedPhoneNumber
      ? 'Cadastrar telefone'
      : status.confirmed
        ? 'WhatsApp confirmado'
        : 'Confirmar WhatsApp'

  return (
    <MenuItem
      onClick={open}
      disabled={loading || Boolean(status?.confirmed)}
      sx={{ minHeight: 44 }}
    >
      <ListItemIcon>
        {loading
          ? <CircularProgress size={18} />
          : status?.confirmed
            ? <CheckCircleRoundedIcon color="success" fontSize="small" />
            : <PhoneAndroidRoundedIcon fontSize="small" />}
      </ListItemIcon>
      {label}
    </MenuItem>
  )
}
