import { Autocomplete, TextField } from '@mui/material'
import type { Unit } from '../types'

const collator = new Intl.Collator('pt-BR', { numeric: true, sensitivity: 'base' })

export function normalizeUnitSearch(value: string) {
  return value.normalize('NFD').replace(/[\u0300-\u036f]/g, '')
    .toLocaleLowerCase('pt-BR').replace(/\s+/g, ' ').trim()
}

export function blockLabel(block: string | null) {
  if (!block?.trim()) return ''
  const value = block.trim()
  return /^bloco\s+/i.test(value) ? value : `Bloco ${value}`
}

export function unitLabel(unit: Unit) {
  const block = blockLabel(unit.block)
  return block ? `${block} · Apto ${unit.identifier}` : `Apto ${unit.identifier}`
}

export function sortUnits(units: Unit[]) {
  return [...units].sort((left, right) => {
    const byBlock = collator.compare(blockLabel(left.block), blockLabel(right.block))
    return byBlock || collator.compare(left.identifier, right.identifier)
  })
}

export function filterUnits(units: Unit[], query: string) {
  const normalized = normalizeUnitSearch(query)
  const blockMatch = /(?:^| )bloco ([^ ]+)/.exec(normalized)
  const tokens = normalized.replace(/(?:^| )bloco [^ ]+/, ' ').split(' ').filter(Boolean)
  if (!tokens.length && !blockMatch) return sortUnits(units)
  return sortUnits(units).filter((unit) => {
    if (blockMatch && !normalizeUnitSearch(blockLabel(unit.block)).endsWith(` ${blockMatch[1]}`)) return false
    if (!blockMatch && tokens.length > 1) {
      const normalizedBlock = normalizeUnitSearch(unit.block ?? '').replace(/^bloco /, '')
      const blockToken = tokens.find((token) => token === normalizedBlock)
      if (blockToken) {
        const unitTokens = tokens.filter((token) => token !== blockToken)
        return unitTokens.every((token) => normalizeUnitSearch(unit.identifier).includes(token))
      }
    }
    const searchable = normalizeUnitSearch(`${blockLabel(unit.block)} ${unit.block ?? ''} apto ${unit.identifier} ${unit.identifier}`)
    return tokens.every((token) => searchable.includes(token))
  })
}

interface Props {
  units: Unit[]
  value: string
  onChange: (unitId: string) => void
  label?: string
  disabled?: boolean
}

export function UnitAutocomplete({ units, value, onChange, label = 'Associar a uma unidade (opcional)', disabled }: Props) {
  const options = sortUnits(units)
  const selected = options.find((unit) => unit.id === value) ?? null
  const hasBlocks = options.some((unit) => Boolean(unit.block?.trim()))

  return (
    <Autocomplete
      options={options}
      value={selected}
      disabled={disabled}
      onChange={(_, unit) => onChange(unit?.id ?? '')}
      getOptionLabel={unitLabel}
      isOptionEqualToValue={(option, current) => option.id === current.id}
      groupBy={hasBlocks ? (unit) => blockLabel(unit.block) || 'Sem bloco' : undefined}
      filterOptions={(available, state) => filterUnits(available, state.inputValue)}
      noOptionsText="Nenhuma unidade encontrada"
      clearText="Limpar"
      openText="Abrir"
      closeText="Fechar"
      renderInput={(params) => <TextField {...params} label={label} />}
    />
  )
}
