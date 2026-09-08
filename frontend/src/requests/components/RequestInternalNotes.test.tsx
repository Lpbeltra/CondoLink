import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { RequestInternalNotes } from './RequestInternalNotes'
import { createInternalNote, deleteInternalNote, editInternalNote, listInternalNotes, type RequestInternalNote } from '../internalNotes'

vi.mock('../internalNotes', () => ({ createInternalNote: vi.fn(), deleteInternalNote: vi.fn(), editInternalNote: vi.fn(), listInternalNotes: vi.fn() }))
const note: RequestInternalNote = { id: 'note', content: 'Verificar com zeladoria.', author: { id: 'manager', fullName: 'Ana' }, createdAt: '2026-09-07T12:00:00Z', updatedAt: null }

describe('RequestInternalNotes', () => {
  beforeEach(() => { vi.resetAllMocks(); vi.mocked(listInternalNotes).mockResolvedValue([]) })

  it('creates, edits and deletes independent notes', async () => {
    vi.mocked(createInternalNote).mockResolvedValue(note)
    vi.mocked(editInternalNote).mockResolvedValue({ ...note, content: 'Zeladoria confirmou.', updatedAt: '2026-09-07T12:15:00Z' })
    vi.mocked(deleteInternalNote).mockResolvedValue(undefined)
    render(<RequestInternalNotes requestId="request" />)
    const user = userEvent.setup()
    await user.type(await screen.findByLabelText('Nova nota interna'), note.content)
    await user.click(screen.getByRole('button', { name: 'Adicionar nota' }))
    expect(createInternalNote).toHaveBeenCalledWith('request', note.content)
    const article = await screen.findByRole('article', { name: 'Nota de Ana' })
    expect(within(article).getByText(note.content)).toBeVisible()
    await user.click(within(article).getByRole('button', { name: 'Editar' }))
    const input = screen.getByLabelText('Editar nota interna')
    await user.clear(input); await user.type(input, 'Zeladoria confirmou.')
    await user.click(screen.getByRole('button', { name: 'Salvar nota' }))
    expect(editInternalNote).toHaveBeenCalledWith('request', 'note', 'Zeladoria confirmou.')
    expect(await screen.findByText('Zeladoria confirmou.')).toBeVisible()
    expect(screen.getByText(/Editada em/)).toBeVisible()
    await user.click(screen.getByRole('button', { name: 'Excluir' }))
    expect(deleteInternalNote).toHaveBeenCalledWith('request', 'note')
    await waitFor(() => expect(screen.queryByRole('article')).not.toBeInTheDocument())
  })

  it('keeps unsaved input and the saved note when editing fails', async () => {
    vi.mocked(listInternalNotes).mockResolvedValue([note])
    vi.mocked(editInternalNote).mockRejectedValue(new Error('offline'))
    render(<RequestInternalNotes requestId="request" />)
    const user = userEvent.setup()
    await user.click(await screen.findByRole('button', { name: 'Editar' }))
    await user.type(screen.getByLabelText('Editar nota interna'), ' Rascunho.')
    await user.click(screen.getByRole('button', { name: 'Salvar nota' }))
    expect(await screen.findByRole('alert')).toBeVisible()
    expect(screen.getByLabelText('Editar nota interna')).toHaveValue(`${note.content} Rascunho.`)
    await user.click(screen.getByRole('button', { name: 'Cancelar edição' }))
    expect(screen.getByText(note.content)).toBeVisible()
  })

  it('keeps the note visible if deletion fails', async () => {
    vi.mocked(listInternalNotes).mockResolvedValue([note])
    vi.mocked(deleteInternalNote).mockRejectedValue(new Error('offline'))
    render(<RequestInternalNotes requestId="request" />)
    await userEvent.setup().click(await screen.findByRole('button', { name: 'Excluir' }))
    expect(await screen.findByRole('alert')).toBeVisible()
    expect(screen.getByText(note.content)).toBeVisible()
  })

  it('does not accept empty notes', async () => {
    render(<RequestInternalNotes requestId="request" />)
    await screen.findByLabelText('Nova nota interna')
    expect(screen.getByRole('button', { name: 'Adicionar nota' })).toBeDisabled()
  })
})
