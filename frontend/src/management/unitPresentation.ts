import type { CondominiumBlock, Unit } from './types'

export const naturalCompare = (left: string, right: string) => left.localeCompare(right, 'pt-BR', { numeric: true, sensitivity: 'base' })
export const sortBlocks = (blocks: CondominiumBlock[]) => [...blocks].sort((a, b) => naturalCompare(a.identifier, b.identifier))
export const filterUnits = (units: Unit[], search: string, blockId: string) => {
  const term = search.trim().toLocaleLowerCase('pt-BR')
  return [...units].filter(unit => (!blockId || unit.blockId === blockId) && (!term || [unit.identifier, unit.block, unit.block ? `${unit.identifier}/${unit.block}` : ''].some(value => value?.toLocaleLowerCase('pt-BR').includes(term))))
    .sort((a, b) => naturalCompare(a.block ?? '', b.block ?? '') || naturalCompare(a.identifier, b.identifier))
}
