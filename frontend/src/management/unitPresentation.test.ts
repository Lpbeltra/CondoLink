import { describe, expect, it } from 'vitest'
import { filterUnits, naturalCompare, sortBlocks } from './unitPresentation'
import type { CondominiumBlock, Unit } from './types'

const unit = (identifier: string, blockId: string | null, block: string | null): Unit => ({ id: `${blockId}-${identifier}`, condominiumId: 'c', identifier, blockId, block, floor: null, description: null, isActive: true, createdAt: '', updatedAt: '' })

describe('unit presentation', () => {
  it('sorts numeric identifiers naturally', () => expect(['10','2','1'].sort(naturalCompare)).toEqual(['1','2','10']))
  it('sorts blocks naturally', () => expect(sortBlocks([{identifier:'Torre 10'},{identifier:'Torre 2'}] as CondominiumBlock[]).map(x=>x.identifier)).toEqual(['Torre 2','Torre 10']))
  it('combines partial search and block filter', () => {
    const units=[unit('301','a','1'),unit('302','b','2'),unit('101','a','1')]
    expect(filterUnits(units,'30','a').map(x=>x.identifier)).toEqual(['301'])
  })
  it('finds compact unit and block notation', () => expect(filterUnits([unit('301','a','1')],'301/1','')).toHaveLength(1))
})
