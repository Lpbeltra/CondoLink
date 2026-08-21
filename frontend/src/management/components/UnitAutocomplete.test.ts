import { describe, expect, it } from 'vitest'
import { blockLabel, filterUnits, sortUnits, unitLabel } from './UnitAutocomplete'
import type { Unit } from '../types'

const unit = (identifier: string, block: string | null = null): Unit => ({
  id: `${block}-${identifier}`, condominiumId: 'c', identifier, blockId: block ? 'b' : null,
  block, floor: null, description: null, isActive: true, createdAt: '', updatedAt: '',
})

describe('UnitAutocomplete helpers', () => {
  const units = [unit('10', '2'), unit('2', '1'), unit('01', '1'), unit('101A', '2'), unit('Térreo', '1'), unit('1', '2')]

  it('groups by block and naturally orders identifiers without changing them', () => {
    expect(sortUnits(units).map((item) => [item.block, item.identifier])).toEqual([
      ['1', '01'], ['1', '2'], ['1', 'Térreo'], ['2', '1'], ['2', '10'], ['2', '101A'],
    ])
  })

  it.each([
    ['101a', ['101A']], ['bloco 2', ['1', '10', '101A']], ['2 101', ['101A']], ['101 2', ['101A']], ['101A bloco 2', ['101A']], ['terreo', ['Térreo']],
  ])('searches normalized unit and block combinations: %s', (query, expected) => {
    expect(filterUnits(units, query).map((item) => item.identifier)).toEqual(expected)
  })

  it('formats condominiums with and without blocks and avoids duplicate prefix', () => {
    expect(unitLabel(unit('1201'))).toBe('Apto 1201')
    expect(unitLabel(unit('1201', '2'))).toBe('Bloco 2 · Apto 1201')
    expect(unitLabel(unit('1201', 'Bloco 2'))).toBe('Bloco 2 · Apto 1201')
    expect(blockLabel('Bloco Norte')).toBe('Bloco Norte')
  })
})
