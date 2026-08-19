import { beforeEach, describe, expect, it, vi } from 'vitest'

const http = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
}))
vi.mock('../services/api', () => ({ api: http }))

import {
  exportActiveResidentsPdf,
  getManagementContext,
  resetMemberTemporaryPassword,
  setManagementContext,
} from './api'

beforeEach(() => vi.clearAllMocks())

describe('management context API', () => {
  it('loads the reconciled context and selects specific or consolidated scope', async () => {
    http.get.mockResolvedValue({ data: {} })
    http.put.mockResolvedValue({ data: {} })

    await getManagementContext()
    await setManagementContext('condominium-id')
    await setManagementContext(null)

    expect(http.get).toHaveBeenCalledWith('/management/context')
    expect(http.put).toHaveBeenNthCalledWith(
      1,
      '/management/context',
      { condominiumId: 'condominium-id' },
    )
    expect(http.put).toHaveBeenNthCalledWith(
      2,
      '/management/context',
      { condominiumId: null },
    )
  })
})

describe('member password API', () => {
  it('resets through the selected condominium and member', async () => {
    http.post.mockResolvedValue({ data: {} })

    await resetMemberTemporaryPassword('condominium-id', 'user-id')

    expect(http.post).toHaveBeenCalledWith(
      '/condominiums/condominium-id/members/user-id/reset-temporary-password',
    )
  })
})

describe('resident PDF export API', () => {
  it('downloads the authenticated blob using the response filename', async () => {
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {})
    const createObjectURL = vi.fn(() => 'blob:residents')
    const revokeObjectURL = vi.fn()
    Object.defineProperty(URL, 'createObjectURL', { value: createObjectURL, configurable: true })
    Object.defineProperty(URL, 'revokeObjectURL', { value: revokeObjectURL, configurable: true })
    http.get.mockResolvedValue({
      data: new Blob(['pdf']),
      headers: { 'content-disposition': 'attachment; filename="moradores-comvy.pdf"' },
    })

    await exportActiveResidentsPdf('condominium-id')

    expect(http.get).toHaveBeenCalledWith(
      '/condominiums/condominium-id/members/export.pdf',
      { responseType: 'blob' },
    )
    expect(click).toHaveBeenCalled()
    expect(revokeObjectURL).toHaveBeenCalledWith('blob:residents')
  })
})
