import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

const { post } = vi.hoisted(() => ({ post: vi.fn() }))
vi.mock('../services/api', () => ({ api: { post } }))
vi.mock('../theme/ThemeModeToggle', () => ({ ThemeModeToggle: () => null }))

import { ResetPasswordPage } from './ResetPasswordPage'

const resetLink = '/reset-password?userId=11111111-1111-1111-1111-111111111111&token=sensitive-token'

function LocationProbe() {
  const location = useLocation()
  return <output data-testid="location">{location.pathname}{location.search}</output>
}

describe('ResetPasswordPage', () => {
  afterEach(cleanup)
  beforeEach(() => post.mockReset())

  function renderPage(entry = resetLink) {
    render(
      <MemoryRouter initialEntries={[entry]}>
        <Routes>
          <Route path="/reset-password" element={<ResetPasswordPage />} />
          <Route path="/forgot-password" element={<h1>Solicitar link</h1>} />
          <Route path="/login" element={<h1>Login</h1>} />
        </Routes>
        <LocationProbe />
      </MemoryRouter>,
    )
  }

  it('does not render a usable form without all link parameters', () => {
    renderPage('/reset-password?userId=11111111-1111-1111-1111-111111111111')
    expect(screen.getByRole('heading', { name: 'Link inválido' })).toBeInTheDocument()
    expect(screen.queryByLabelText(/^Nova senha/)).not.toBeInTheDocument()
  })

  it('keeps the token out of the rendered interface and validates confirmation', async () => {
    renderPage()
    expect(screen.queryByText('sensitive-token')).not.toBeInTheDocument()
    const user = userEvent.setup()
    await user.type(screen.getByLabelText(/^Nova senha/), 'NovaSenha1')
    await user.type(screen.getByLabelText(/^Confirmar nova senha/), 'OutraSenha1')
    expect(screen.getByText('A confirmação da senha não confere.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Redefinir senha' })).toBeDisabled()
  })

  it('submits the real reset contract and clears the sensitive query after success', async () => {
    post.mockResolvedValueOnce({ data: {} })
    renderPage()
    const user = userEvent.setup()
    await user.type(screen.getByLabelText(/^Nova senha/), 'NovaSenha1')
    await user.type(screen.getByLabelText(/^Confirmar nova senha/), 'NovaSenha1')
    await user.click(screen.getByRole('button', { name: 'Redefinir senha' }))

    expect(post).toHaveBeenCalledWith('/auth/reset-password', {
      userId: '11111111-1111-1111-1111-111111111111', token: 'sensitive-token', password: 'NovaSenha1', confirmation: 'NovaSenha1',
    })
    expect(await screen.findByRole('heading', { name: 'Senha redefinida' })).toBeInTheDocument()
    expect(screen.getByTestId('location')).toHaveTextContent(/^\/reset-password$/)
  })

  it('distinguishes a rejected password from an invalid link', async () => {
    post.mockRejectedValueOnce({ response: { status: 400, data: { error: 'A nova senha não atende aos requisitos de segurança.' } } })
    renderPage()
    fireEvent.change(screen.getByLabelText(/^Nova senha/), { target: { value: 'NovaSenha1' } })
    fireEvent.change(screen.getByLabelText(/^Confirmar nova senha/), { target: { value: 'NovaSenha1' } })
    fireEvent.click(screen.getByRole('button', { name: 'Redefinir senha' }))
    expect(await screen.findByText('Ao menos 8 caracteres, com maiúscula, minúscula e número.')).toBeInTheDocument()

    post.mockRejectedValueOnce({ response: { status: 400, data: { error: 'O link é inválido, expirou ou já foi utilizado.' } } })
    fireEvent.click(screen.getByRole('button', { name: 'Redefinir senha' }))
    expect(await screen.findByRole('heading', { name: 'Link inválido' })).toBeInTheDocument()
    expect(screen.getByText('Este link de redefinição não é mais válido. Solicite um novo link.')).toBeInTheDocument()
  })

  it('navigates to request a new link', async () => {
    renderPage('/reset-password')
    await userEvent.click(screen.getByRole('link', { name: 'Solicitar novo link' }))
    expect(screen.getByRole('heading', { name: 'Solicitar link' })).toBeInTheDocument()
  })
})
