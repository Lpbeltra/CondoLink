/**
 * Client-side JWT expiry inspection.
 *
 * The server is always the authority on token validity — this only lets the app
 * avoid rendering an authenticated shell around a token that is already dead,
 * and drop the session proactively instead of waiting for the next 401.
 */

/** Treat a token as expired slightly early to avoid races with in-flight calls. */
export const expiryLeewaySeconds = 30

function decodePayload(token: string): Record<string, unknown> | null {
  try {
    const payloadPart = token.split('.')[1]
    if (!payloadPart) return null
    const normalized = payloadPart
      .replace(/-/g, '+')
      .replace(/_/g, '/')
      .padEnd(Math.ceil(payloadPart.length / 4) * 4, '=')
    const bytes = Uint8Array.from(atob(normalized), (character) => character.charCodeAt(0))
    return JSON.parse(new TextDecoder().decode(bytes)) as Record<string, unknown>
  } catch {
    return null
  }
}

/** Returns the `exp` claim in milliseconds, or null when absent/unreadable. */
export function getTokenExpiration(token: string): number | null {
  const payload = decodePayload(token)
  const exp = payload?.exp
  if (typeof exp !== 'number' || !Number.isFinite(exp)) return null
  return exp * 1000
}

/**
 * A token with no readable `exp` is NOT treated as expired: the server still
 * validates it, and failing closed here would lock out users over a decoding
 * quirk. Only a definite, past expiry counts.
 */
export function isTokenExpired(token: string, now: number = Date.now()): boolean {
  const expiresAt = getTokenExpiration(token)
  if (expiresAt === null) return false
  return expiresAt - expiryLeewaySeconds * 1000 <= now
}

/** Milliseconds until expiry (leeway applied), or null when unknown. */
export function getMillisecondsUntilExpiry(
  token: string,
  now: number = Date.now(),
): number | null {
  const expiresAt = getTokenExpiration(token)
  if (expiresAt === null) return null
  return Math.max(0, expiresAt - expiryLeewaySeconds * 1000 - now)
}
