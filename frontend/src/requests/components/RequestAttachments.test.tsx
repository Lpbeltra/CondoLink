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
    attachmentApi.getRequestAttachmentBlob.mockResolvedValue(new Blob(['image'], { type: 'image/jpeg' }))
    Object.defineProperty(URL, 'createObjectURL', { configurable: true, value: vi.fn(() => 'blob:image') })
    Object.defineProperty(URL, 'revokeObjectURL', { configurable: true, value: vi.fn() })
  })

  it('selects multiple files and removes one before upload', async () => {
    const user = userEvent.setup()
    const { container } = render(
      <RequestAttachments requestId="request-id" readOnly={false} />,
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
      <RequestAttachments requestId="request-id" readOnly={false} />,
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
      <RequestAttachments requestId="request-id" readOnly={false} />,
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

  it('keeps existing downloads visible but hides mutations in read-only mode', async () => {
    attachmentApi.listRequestAttachments.mockResolvedValue([attachment()])

    render(<RequestAttachments requestId="request-id" readOnly />)

    expect(await screen.findByRole('button', {
      name: 'Baixar documento.pdf',
    })).toBeInTheDocument()
    expect(screen.queryByRole('button', {
      name: 'Excluir documento.pdf',
    })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', {
      name: 'Adicionar anexos',
    })).not.toBeInTheDocument()
  })

  it('shows audio with an on-demand preview and keeps attachments newest first', async () => {
    attachmentApi.listRequestAttachments.mockResolvedValue([
      { ...attachment('new'), originalFileName: 'novo.pdf', createdAt: '2026-08-03T10:00:00Z' },
      { ...attachment('audio'), originalFileName: 'audio.ogg', contentType: 'audio/ogg', createdAt: '2026-08-02T10:00:00Z' },
      { ...attachment('old'), originalFileName: 'antigo.pdf', createdAt: '2026-08-01T10:00:00Z' },
    ])
    render(<RequestAttachments requestId="request-id" readOnly />)

    const newest = await screen.findByText('novo.pdf')
    const oldest = screen.getByText('antigo.pdf')
    expect(screen.getByText('audio.ogg')).toBeInTheDocument()
    expect(screen.getAllByRole('button', { name: 'Visualizar' })).not.toHaveLength(0)
    expect(newest.compareDocumentPosition(oldest) & Node.DOCUMENT_POSITION_FOLLOWING)
      .toBeTruthy()
  })

  it.each([
    ['audio.ogg', 'audio/ogg', 'audio'],
    ['video.mp4', 'video/mp4', 'video'],
    ['documento.pdf', 'application/pdf', 'iframe'],
  ])('loads %s only after preview is requested and cleans its Blob URL',
    async (name, contentType, element) => {
      attachmentApi.listRequestAttachments.mockResolvedValue([
        { ...attachment('preview'), originalFileName: name, contentType },
      ])
      const user = userEvent.setup()
      render(<RequestAttachments requestId="request-id" readOnly />)
      expect(attachmentApi.getRequestAttachmentBlob).not.toHaveBeenCalled()
      await user.click(await screen.findByRole('button', { name: 'Visualizar' }))
      await waitFor(() => expect(document.querySelector(element)).toBeInTheDocument())
      expect(attachmentApi.getRequestAttachmentBlob).toHaveBeenCalledWith(
        '/request-attachments/preview/content')
      await user.click(screen.getByRole('button', { name: 'Fechar' }))
      await waitFor(() => expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:image'))
    })

  it('presents images as a responsive clickable gallery', async () => {
    attachmentApi.listRequestAttachments.mockResolvedValue([
      { ...attachment('one'), originalFileName: 'fachada.jpg', contentType: 'image/jpeg' },
      { ...attachment('two'), originalFileName: 'portão.jpg', contentType: 'image/jpeg' },
    ])
    render(<RequestAttachments requestId="request-id" readOnly />)

    expect(await screen.findByText('Galeria de imagens')).toBeVisible()
    expect(await screen.findByRole('button', { name: 'Ampliar fachada.jpg' })).toBeVisible()
    expect(screen.getByRole('button', { name: 'Ampliar portão.jpg' })).toBeVisible()
    expect(screen.queryByText('Nenhum anexo enviado.')).not.toBeInTheDocument()
  })
})
