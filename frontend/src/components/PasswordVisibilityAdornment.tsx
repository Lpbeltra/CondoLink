import VisibilityRoundedIcon from '@mui/icons-material/VisibilityRounded'
import VisibilityOffRoundedIcon from '@mui/icons-material/VisibilityOffRounded'
import { IconButton, InputAdornment } from '@mui/material'

interface Props {
  visible: boolean
  onToggle: () => void
}

export function PasswordVisibilityAdornment({ visible, onToggle }: Props) {
  return (
    <InputAdornment position="end">
      <IconButton
        type="button"
        edge="end"
        aria-label={visible ? 'Ocultar senha' : 'Exibir senha'}
        onClick={onToggle}
      >
        {visible
          ? <VisibilityOffRoundedIcon />
          : <VisibilityRoundedIcon />}
      </IconButton>
    </InputAdornment>
  )
}
