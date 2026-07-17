import { beforeEach, describe, expect, it, vi } from 'vitest'
import { clearStoredToken, getStoredToken, storeToken } from './authStorage'

describe('authentication storage', () => {
  const values = new Map<string, string>()

  beforeEach(() => {
    values.clear()
    vi.stubGlobal('localStorage', {
      getItem: (key: string) => values.get(key) ?? null,
      setItem: (key: string, value: string) => values.set(key, value),
      removeItem: (key: string) => values.delete(key),
    })
  })

  it('stores and restores the access token', () => {
    storeToken('token')
    expect(getStoredToken()).toBe('token')
  })

  it('removes the token on logout', () => {
    storeToken('token')
    clearStoredToken()
    expect(getStoredToken()).toBeNull()
  })
})
