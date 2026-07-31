import { beforeEach, describe, expect, it, vi } from 'vitest'

const http = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
}))

vi.mock('../services/api', () => ({ api: http }))

import {
  confirmPhoneVerification,
  getPhoneVerificationStatus,
  startPhoneVerification,
} from './api'

describe('authenticated phone verification API', () => {
  beforeEach(() => {
    http.get.mockReset()
    http.post.mockReset()
    http.get.mockResolvedValue({ data: {} })
    http.post.mockResolvedValue({ data: {} })
  })

  it('uses only the authenticated registration routes', async () => {
    await getPhoneVerificationStatus()
    await startPhoneVerification()
    await confirmPhoneVerification('123456')

    expect(http.get).toHaveBeenCalledWith('/users/me/phone-verification')
    expect(http.post).toHaveBeenNthCalledWith(
      1,
      '/users/me/phone-verification',
    )
    expect(http.post).toHaveBeenNthCalledWith(
      2,
      '/users/me/phone-verification/confirm',
      { code: '123456' },
    )
    expect(http.post).not.toHaveBeenCalledWith(
      '/auth/whatsapp/request-code',
      expect.anything(),
    )
  })
})
