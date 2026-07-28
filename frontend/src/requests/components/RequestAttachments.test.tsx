import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { RequestAttachment } from '../types'

const attachmentApi = vi.hoisted(() => ({
  deleteRequestAttachment: vi.fn(),
  getRequestAttachmentBlob: vi.fn(),
  listRequestAttachments: vi.fn(),
  uploadRequestAttachments: vi.fn(),
}))

vi.mock('../api', () => attachmentApi)

import { RequestAttachments } from './RequestAttachments'

function attachment(id = 'attachment-id'): RequestAttachment {
  return {
    id,
    requestId: 'request-id',
    originalFileName: 'documento.pdf',
    contentType: 'application/pdf',
    fileSize: 2048,
    uploadedBy: { id: 'user-id', fullName: 'Maria Silva' },
    createdAt: '2026-07-27T10:00:00Z',
    contentUrl: `/request-attachments/${id}/content`,
  }
}

describe('RequestAttachments', () => {
  beforeEach(() => {
    attachmentApi.listRequestAttachments.mockResolvedValue([])
    attachmentApi.deleteRequestAttachment.mockResolvedValue(undefined)
  })

  it('selects multiple files and removes one before upload', async () => {
    const user = userEvent.setup()
    const { container } = render(
      <RequestAttachments requestId="request-id" cancelled={false} />,
    )
    const input = container.querySelector<HTMLInputElement>(
      'input[type="file"]',
    )!
    const image = new File(['image'], 'foto.jpg', { type: 'image/jpeg' })
    const pdf = new File(['pdf'], 'documento.pdf', {
      type: 'application/pdf',
    })

    await user.upload(input, [image, pdf])

    expect(screen.getByText('2 arquivos selecionados')).toBeInTheDocument()
    expect(screen.getByText('foto.jpg')).toBeInTheDocument()
    expect(screen.getByText('documento.pdf')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Remover foto.jpg' }))

    expect(screen.getByText('1 arquivo selecionado')).toBeInTheDocument()
    expect(screen.queryByText('foto.jpg')).not.toBeInTheDocument()
  })

  it('shows progress, blocks selection changes, appends the upload and clears selection', async () => {
    const uploaded = attachment()
    let completeUpload!: (value: RequestAttachment[]) => void
    attachmentApi.uploadRequestAttachments.mockImplementation(
      async (
        _requestId: string,
        _files: File[],
        onProgress: (loaded: number, total?: number) => void,
      ) => {
        onProgress(42, 100)
        return await new Promise<RequestAttachment[]>(resolve => {
          completeUpload = resolve
        })
      },
    )

    const user = userEvent.setup()
    const { container } = render(
      <RequestAttachments requestId="request-id" cancelled={false} />,
    )
    const input = container.querySelector<HTMLInputElement>(
      'input[type="file"]',
    )!
    await user.upload(
      input,
      new File(['pdf'], 'documento.pdf', { type: 'application/pdf' }),
    )
    await user.click(screen.getByRole('button', { name: 'Enviar arquivos' }))

    expect(await screen.findByText('42%')).toBeInTheDocument()
    expect(screen.getByRole('button', {
      name: 'Remover documento.pdf',
    })).toBeDisabled()
    expect(screen.queryByRole('button', {
      name: 'Cancelar seleção',
    })).not.toBeInTheDocument()

    completeUpload([uploaded])

    await waitFor(() => {
      expect(screen.queryByText('1 arquivo selecionado')).not.toBeInTheDocument()
    })
    expect(screen.getByText(/Maria Silva/)).toBeInTheDocument()
    expect(attachmentApi.listRequestAttachments).toHaveBeenCalledTimes(1)
  })

  it('confirms deletion and removes the attachment locally', async () => {
    attachmentApi.listRequestAttachments.mockResolvedValue([attachment()])
    const user = userEvent.setup()
    render(
      <RequestAttachments requestId="request-id" cancelled={false} />,
    )

    await user.click(await screen.findByRole('button', {
      name: 'Excluir documento.pdf',
    }))
    expect(screen.getByText('Excluir anexo?')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Excluir' }))

    await waitFor(() => {
      expect(screen.queryByText('documento.pdf')).not.toBeInTheDocument()
    })
    expect(attachmentApi.deleteRequestAttachment).toHaveBeenCalledWith(
      'attachment-id',
    )
    expect(attachmentApi.listRequestAttachments).toHaveBeenCalledTimes(1)
  })
})
