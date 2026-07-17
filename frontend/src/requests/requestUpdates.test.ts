import { describe, expect, it } from 'vitest'
import { getUpdateMarkerColor, newestStatusHistoryFirst } from './requestUpdates'
import type { RequestMessage, StatusHistoryItem } from './types'

const message = (isManager: boolean): RequestMessage => ({
  id: 'message', requestId: 'request', content: 'Atualização', createdAt: '2026-07-16T12:00:00Z',
  author: { id: 'author', fullName: 'Pessoa', isManager },
})

describe('request updates presentation', () => {
  it('uses green for resident updates', () => expect(getUpdateMarkerColor(message(false))).toBe('success.main'))
  it('uses blue for management updates', () => expect(getUpdateMarkerColor(message(true))).toBe('primary.main'))

  it('orders status history from newest to oldest without mutating its input', () => {
    const history = [
      { id: 'old', createdAt: '2026-07-16T10:00:00Z' },
      { id: 'new', createdAt: '2026-07-16T12:00:00Z' },
    ] as StatusHistoryItem[]
    expect(newestStatusHistoryFirst(history).map((item) => item.id)).toEqual(['new', 'old'])
    expect(history.map((item) => item.id)).toEqual(['old', 'new'])
  })
})
