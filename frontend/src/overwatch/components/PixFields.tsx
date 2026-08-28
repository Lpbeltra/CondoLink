import { MenuItem, TextField } from '@mui/material'
export type PixKeyType = 'Cpf' | 'Cnpj' | 'Email' | 'Phone' | 'Random'
export const pixKeyTypeOptions: ReadonlyArray<{ value: PixKeyType; label: string }> = [
  { value: 'Cpf', label: 'CPF' }, { value: 'Cnpj', label: 'CNPJ' },
  { value: 'Email', label: 'E-mail' }, { value: 'Phone', label: 'Telefone' },
  { value: 'Random', label: 'Chave aleatória' },
]
export function PixFields({ type, pixKey, onTypeChange, onKeyChange }: {
  type: PixKeyType | ''; pixKey: string
  onTypeChange: (value: PixKeyType | '') => void; onKeyChange: (value: string) => void
}) {
  return <>
    <TextField select label="Tipo da chave PIX" value={type} onChange={event => onTypeChange(event.target.value as PixKeyType | '')}>
      <MenuItem value="">Não informado</MenuItem>
      {pixKeyTypeOptions.map(option => <MenuItem key={option.value} value={option.value}>{option.label}</MenuItem>)}
    </TextField>
    <TextField label="Chave PIX" value={pixKey} onChange={event => onKeyChange(event.target.value)} disabled={!type}
      slotProps={{ htmlInput: { maxLength: 200 } }} />
  </>
}
export function validatePix(type: PixKeyType | null, key: string | null) {
  if ((type && !key?.trim()) || (!type && key?.trim())) return 'Informe o tipo e a chave PIX juntos.'
  if ((key?.trim().length ?? 0) > 200) return 'A chave PIX deve possuir no máximo 200 caracteres.'
  return null
}
