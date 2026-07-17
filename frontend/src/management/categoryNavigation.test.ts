import { describe, expect, it } from 'vitest'
import { categoryRequestsUrl, validCategoryId } from './categoryNavigation'
import type { Category } from './types'

const categories=[{id:'category-id'}] as Category[]
describe('category attendance navigation',()=>{
  it('sends the category id in the attendance URL',()=>expect(categoryRequestsUrl('category-id')).toBe('/management/requests?categoryId=category-id'))
  it('accepts only a category from the current condominium response',()=>{expect(validCategoryId('category-id',categories)).toBe('category-id');expect(validCategoryId('invalid',categories)).toBe('')})
})
