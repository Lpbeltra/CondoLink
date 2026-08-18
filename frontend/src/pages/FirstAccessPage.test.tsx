import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'

const { post } = vi.hoisted(() => ({ post: vi.fn() }))
vi.mock('../services/api', () => ({ api: { post } }))
import { FirstAccessPage } from './FirstAccessPage'

describe('FirstAccessPage', () => {
  beforeEach(() => post.mockReset())

  it('validates the link and creates the password', async () => {
    post.mockResolvedValueOnce({ data: { valid: true } }).mockResolvedValueOnce({ data: {} })
    render(<MemoryRouter initialEntries={['/primeiro-acesso?userId=11111111-1111-1111-1111-111111111111&token=abc']}><FirstAccessPage /></MemoryRouter>)
    await screen.findByRole('heading', { name: 'Crie sua senha' })
    const user = userEvent.setup()
    await user.type(screen.getByLabelText(/^Nova senha/), 'NovaSenha1')
    await user.type(screen.getByLabelText(/^Confirmar senha/), 'NovaSenha1')
    await user.click(screen.getByRole('button', { name: 'Criar senha' }))
    expect(await screen.findByText('Senha criada com sucesso.')).toBeInTheDocument()
  })

  it('shows a safe error for an invalid token', async () => {
    post.mockRejectedValueOnce(new Error('invalid'))
    render(<MemoryRouter initialEntries={['/primeiro-acesso?userId=11111111-1111-1111-1111-111111111111&token=bad']}><FirstAccessPage /></MemoryRouter>)
    await waitFor(() => expect(screen.getByText(/inválido, expirou ou já foi utilizado/i)).toBeInTheDocument())
  })
})
