import type { RequestMessage, StatusHistoryItem } from './types'

export function getUpdateMarkerColor(message: RequestMessage) {
  return message.author.isManager ? 'primary.main' : 'success.main'
}

export function newestStatusHistoryFirst(history: StatusHistoryItem[]) {
  return [...history].sort((left, right) => {
    const byDate = new Date(right.createdAt).getTime() - new Date(left.createdAt).getTime()
    return byDate || right.id.localeCompare(left.id)
  })
}
