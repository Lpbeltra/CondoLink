import type { Category } from './types'

export function filterCategories(categories: Category[], search: string) {
  const normalize = (value: string) => value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLocaleLowerCase('pt-BR')
  const term = normalize(search.trim())
  return [...categories].filter(category => !term || normalize(category.name).includes(term)).sort((left, right) => left.name.localeCompare(right.name, 'pt-BR', { sensitivity: 'base' }))
}
