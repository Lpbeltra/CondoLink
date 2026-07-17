import { describe, expect, it } from 'vitest'
import { filterCategories } from './categoryPresentation'
import type { Category } from './types'

const category = (name: string, requestCount: number): Category => ({ id:name, condominiumId:'c', name, description:null, requestCount })
describe('category presentation', () => {
  it('filters partially without case sensitivity and keeps alphabetical order', () => {
    expect(filterCategories([category('Eletrônicos',2),category('Elétrica',18),category('Limpeza',0)],'ELE').map(item=>item.name)).toEqual(['Elétrica','Eletrônicos'])
  })
  it('preserves the usage count', () => expect(filterCategories([category('Elétrica',18)],'')[0].requestCount).toBe(18))
})
