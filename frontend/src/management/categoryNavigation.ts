import type { Category } from './types'

export const categoryRequestsUrl = (categoryId: string) => `/management/requests?categoryId=${encodeURIComponent(categoryId)}`
export const validCategoryId = (requestedId: string | null, categories: Category[]) => requestedId && categories.some(category => category.id === requestedId) ? requestedId : ''
