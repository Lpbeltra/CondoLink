import { describe, expect, it } from 'vitest'
import {
  badgeCap,
  countUnread,
  formatBadgeCount,
  formatRelativeTime,
  isUnread,
  markAllReadLocally,
  markReadLocally,
  notificationLink,
} from './presentation'
import type { AppNotification } from './types'

function notification(overrides: Partial<AppNotification> = {}): AppNotification {
  return {
    id: 'n1',
    condominiumId: 'c1',
    type: 'RequestCreated',
    title: 'Nova solicitação',
    body: 'Manutenção: Vazamento',
    requestId: 'r1',
    managementCompanyRequestId: null,
    createdAt: '2026-05-01T12:00:00.000Z',
    readAt: null,
    ...overrides,
  }
}

describe('formatBadgeCount', () => {
  it('renders nothing when there is nothing unread', () => {
    expect(formatBadgeCount(0)).toBe('')
    expect(formatBadgeCount(-1)).toBe('')
  })

  it('renders the exact count up to the cap', () => {
    expect(formatBadgeCount(1)).toBe('1')
    expect(formatBadgeCount(badgeCap)).toBe(String(badgeCap))
  })

  it('caps large counts', () => {
    expect(formatBadgeCount(badgeCap + 1)).toBe(`${badgeCap}+`)
    expect(formatBadgeCount(250)).toBe(`${badgeCap}+`)
  })
})

describe('formatRelativeTime', () => {
  const now = Date.parse('2026-05-01T12:00:00.000Z')

  it('says "agora" under a minute rather than "0 min"', () => {
    expect(formatRelativeTime('2026-05-01T11:59:30.000Z', now)).toBe('agora')
  })

  it('renders minutes, hours and days', () => {
    expect(formatRelativeTime('2026-05-01T11:30:00.000Z', now)).toBe('30 min')
    expect(formatRelativeTime('2026-05-01T09:00:00.000Z', now)).toBe('3 h')
    expect(formatRelativeTime('2026-04-29T12:00:00.000Z', now)).toBe('2 d')
  })

  it('renders weeks and months', () => {
    expect(formatRelativeTime('2026-04-17T12:00:00.000Z', now)).toBe('2 sem')
    expect(formatRelativeTime('2026-02-01T12:00:00.000Z', now)).toBe('2 meses')
  })

  it('uses the singular for one month', () => {
    expect(formatRelativeTime('2026-03-27T12:00:00.000Z', now)).toBe('1 mes')
  })

  it('treats a future timestamp as now instead of a negative duration', () => {
    expect(formatRelativeTime('2026-05-02T12:00:00.000Z', now)).toBe('agora')
  })

  it('returns an empty string for an unparseable date', () => {
    expect(formatRelativeTime('not-a-date', now)).toBe('')
  })
})

describe('unread helpers', () => {
  it('detects unread by a null readAt', () => {
    expect(isUnread(notification())).toBe(true)
    expect(isUnread(notification({ readAt: '2026-05-01T13:00:00.000Z' }))).toBe(false)
  })

  it('counts only unread items', () => {
    expect(countUnread([
      notification({ id: 'a' }),
      notification({ id: 'b', readAt: '2026-05-01T13:00:00.000Z' }),
      notification({ id: 'c' }),
    ])).toBe(2)
  })
})

describe('notificationLink', () => {
  it('points managers at the management view', () => {
    expect(notificationLink(notification(), true)).toBe('/management/requests/r1')
  })

  it('points residents at their own view', () => {
    expect(notificationLink(notification(), false)).toBe('/requests/r1')
  })

  it('returns null when there is nothing to open', () => {
    expect(notificationLink(notification({ requestId: null }), false)).toBeNull()
  })

  it('points administradora-facing types at the administrator portal regardless of viewer role', () => {
    for (const type of [
      'ManagementCompanyRequestCreated',
      'ManagementCompanyRequestManagerReplied',
      'ManagementCompanyRequestCancelled',
    ] as const) {
      const target = notification({ type, requestId: null, managementCompanyRequestId: 'mcr1' })
      expect(notificationLink(target, true)).toBe('/administrator/requests/mcr1')
      expect(notificationLink(target, false)).toBe('/administrator/requests/mcr1')
    }
  })

  it('points gestão-facing types at the management portal regardless of viewer role', () => {
    for (const type of [
      'ManagementCompanyRequestInfoRequested',
      'ManagementCompanyRequestCompleted',
    ] as const) {
      const target = notification({ type, requestId: null, managementCompanyRequestId: 'mcr1' })
      expect(notificationLink(target, true)).toBe('/management/administrator/mcr1')
      expect(notificationLink(target, false)).toBe('/management/administrator/mcr1')
    }
  })
})

describe('markReadLocally', () => {
  const readAt = '2026-05-01T13:00:00.000Z'

  it('marks the targeted notification', () => {
    const result = markReadLocally([notification({ id: 'a' })], 'a', readAt)
    expect(result[0].readAt).toBe(readAt)
  })

  it('leaves other notifications untouched', () => {
    const result = markReadLocally(
      [notification({ id: 'a' }), notification({ id: 'b' })],
      'a',
      readAt,
    )
    expect(result[1].readAt).toBeNull()
  })

  it('does not move an existing read timestamp', () => {
    const original = '2026-05-01T12:30:00.000Z'
    const result = markReadLocally(
      [notification({ id: 'a', readAt: original })],
      'a',
      readAt,
    )
    expect(result[0].readAt).toBe(original)
  })

  it('does not mutate the input array', () => {
    const input = [notification({ id: 'a' })]
    markReadLocally(input, 'a', readAt)
    expect(input[0].readAt).toBeNull()
  })
})

describe('markAllReadLocally', () => {
  const readAt = '2026-05-01T13:00:00.000Z'

  it('marks every unread notification', () => {
    const result = markAllReadLocally(
      [notification({ id: 'a' }), notification({ id: 'b' })],
      readAt,
    )
    expect(result.every((item) => item.readAt === readAt)).toBe(true)
  })

  it('preserves timestamps of already-read notifications', () => {
    const original = '2026-05-01T12:30:00.000Z'
    const result = markAllReadLocally(
      [notification({ id: 'a', readAt: original }), notification({ id: 'b' })],
      readAt,
    )
    expect(result[0].readAt).toBe(original)
    expect(result[1].readAt).toBe(readAt)
  })
})
