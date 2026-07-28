import { beforeEach, describe, expect, it, vi } from 'vitest'

const http = vi.hoisted(() => ({
  delete: vi.fn(),
  get: vi.fn(),
  post: vi.fn(),
}))
vi.mock('../services/api', () => ({ api: http }))

import {
  deleteRequestAttachment,
  getRequestAttachmentBlob,
  listManagementRequests,
  uploadRequestAttachments,
} from './api'

describe('management requests API', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('omits a condominium in consolidated mode and sends it in specific mode', async () => {
    http.get.mockResolvedValue({ data: {} })

    await listManagementRequests({ status: 'Open' })
    await listManagementRequests({
      status: 'Open',
      condominiumId: 'condominium-id',
    })

    expect(http.get).toHaveBeenNthCalledWith(
      1,
      '/management/requests',
      { params: { status: 'Open' } },
    )
    expect(http.get).toHaveBeenNthCalledWith(
      2,
      '/management/requests',
      {
        params: {
          status: 'Open',
          condominiumId: 'condominium-id',
        },
      },
    )
  })
})

describe('request attachment API', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('posts every selected file under the files multipart field and reports progress', async () => {
    const first = new File(['image'], 'foto.jpg', { type: 'image/jpeg' })
    const second = new File(['pdf'], 'documento.pdf', {
      type: 'application/pdf',
    })
    const progress = vi.fn()
    http.post.mockImplementation(
      async (_url: string, _form: FormData, config: {
        onUploadProgress(event: { loaded: number; total?: number }): void
      }) => {
        config.onUploadProgress({ loaded: 5, total: 10 })
        return { data: [] }
      },
    )

    await uploadRequestAttachments(
      'request-id',
      [first, second],
      progress,
    )

    const [url, form, config] = http.post.mock.calls[0]
    expect(url).toBe('/requests/request-id/attachments')
    expect(form).toBeInstanceOf(FormData)
    expect(form.getAll('files')).toEqual([first, second])
    expect(config.timeout).toBe(5 * 60 * 1000)
    expect(progress).toHaveBeenCalledWith(5, 10)
  })

  it('downloads content as an authenticated blob request', async () => {
    const blob = new Blob(['content'], { type: 'application/pdf' })
    http.get.mockResolvedValue({ data: blob })

    await expect(
      getRequestAttachmentBlob('/request-attachments/id/content'),
    ).resolves.toBe(blob)
    expect(http.get).toHaveBeenCalledWith(
      '/request-attachments/id/content',
      { responseType: 'blob' },
    )
  })

  it('deletes the selected attachment', async () => {
    http.delete.mockResolvedValue({})

    await deleteRequestAttachment('attachment-id')

    expect(http.delete).toHaveBeenCalledWith(
      '/request-attachments/attachment-id',
    )
  })
})
