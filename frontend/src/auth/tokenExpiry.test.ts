import { describe, expect, it } from 'vitest'
import {
  expiryLeewaySeconds,
  getMillisecondsUntilExpiry,
  getTokenExpiration,
  isTokenExpired,
} from './tokenExpiry'

/** Builds an unsigned JWT whose payload carries the given claims. */
function makeToken(payload: Record<string, unknown>): string {
  const encode = (value: object) =>
    btoa(JSON.stringify(value)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
  return `${encode({ alg: 'HS256', typ: 'JWT' })}.${encode(payload)}.signature`
}

const now = Date.UTC(2026, 0, 1, 12, 0, 0)
const seconds = (ms: number) => Math.floor(ms / 1000)

describe('getTokenExpiration', () => {
  it('reads the exp claim as milliseconds', () => {
    const expiresAt = now + 3_600_000
    expect(getTokenExpiration(makeToken({ exp: seconds(expiresAt) })))
      .toBe(seconds(expiresAt) * 1000)
  })

  it('returns null when exp is absent', () => {
    expect(getTokenExpiration(makeToken({ sub: 'abc' }))).toBeNull()
  })

  it('returns null for a non-numeric exp', () => {
    expect(getTokenExpiration(makeToken({ exp: 'soon' }))).toBeNull()
  })

  it('returns null for malformed tokens instead of throwing', () => {
    expect(getTokenExpiration('not-a-jwt')).toBeNull()
    expect(getTokenExpiration('')).toBeNull()
    expect(getTokenExpiration('a.b')).toBeNull()
    expect(getTokenExpiration('a.!!!not-base64!!!.c')).toBeNull()
  })
})

describe('isTokenExpired', () => {
  it('treats a comfortably future token as valid', () => {
    expect(isTokenExpired(makeToken({ exp: seconds(now + 3_600_000) }), now)).toBe(false)
  })

  it('treats a past token as expired', () => {
    expect(isTokenExpired(makeToken({ exp: seconds(now - 1_000) }), now)).toBe(true)
  })

  it('expires a token that dies inside the leeway window', () => {
    const insideLeeway = now + (expiryLeewaySeconds - 5) * 1000
    expect(isTokenExpired(makeToken({ exp: seconds(insideLeeway) }), now)).toBe(true)
  })

  it('keeps a token that outlives the leeway window', () => {
    const outsideLeeway = now + (expiryLeewaySeconds + 60) * 1000
    expect(isTokenExpired(makeToken({ exp: seconds(outsideLeeway) }), now)).toBe(false)
  })

  it('does not expire a token whose exp cannot be read', () => {
    // Failing closed here would lock users out over a decoding quirk; the API
    // remains the authority on validity.
    expect(isTokenExpired(makeToken({ sub: 'abc' }), now)).toBe(false)
    expect(isTokenExpired('garbage', now)).toBe(false)
  })
})

describe('getMillisecondsUntilExpiry', () => {
  it('reports the remaining time minus the leeway', () => {
    const token = makeToken({ exp: seconds(now + 600_000) })
    expect(getMillisecondsUntilExpiry(token, now))
      .toBe(600_000 - expiryLeewaySeconds * 1000)
  })

  it('never reports a negative duration', () => {
    expect(getMillisecondsUntilExpiry(makeToken({ exp: seconds(now - 90_000) }), now)).toBe(0)
  })

  it('returns null when the expiry is unknown', () => {
    expect(getMillisecondsUntilExpiry(makeToken({ sub: 'x' }), now)).toBeNull()
  })
})
