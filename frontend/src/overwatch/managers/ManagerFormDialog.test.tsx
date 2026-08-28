import { fireEvent, render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { ManagerFormDialog } from './ManagerFormDialog'
import type { OverwatchManager } from './types'

const manager: OverwatchManager = {
  id: 'manager-1', fullName: 'Síndico', email: 'sindico@example.com',
  phoneNumber: null, cpf: null, cnpj: null, address: null, city: null,
  state: null, pixKeyType: 'Email', pixKey: 'pix@example.com', isActive: true,
  condominiumCount: 2, createdAt: '2026-01-01', updatedAt: '2026-01-01',
}

describe('ManagerFormDialog PIX', () => {
  it('shows and loads the existing PIX fields', () => {
    render(<ManagerFormDialog open manager={manager} isSaving={false} error="" onClose={vi.fn()} onSubmit={vi.fn()} />)
    expect(screen.getByLabelText('Tipo da chave PIX')).toHaveTextContent('E-mail')
    expect(screen.getByLabelText('Chave PIX')).toHaveValue('pix@example.com')
  })

  it('edits and clears PIX through the existing manager submit flow', () => {
    const onSubmit = vi.fn().mockResolvedValue(undefined)
    const view = render(<ManagerFormDialog open manager={manager} isSaving={false} error="" onClose={vi.fn()} onSubmit={onSubmit} />)
    fireEvent.change(screen.getByLabelText('Chave PIX'), { target: { value: 'novo@example.com' } })
    fireEvent.submit(screen.getByRole('button', { name: 'Salvar' }).closest('form')!)
    expect(onSubmit).toHaveBeenLastCalledWith(expect.objectContaining({ pixKeyType: 'Email', pixKey: 'novo@example.com' }))

    view.rerender(<ManagerFormDialog open manager={{ ...manager, pixKeyType: null, pixKey: null }} isSaving={false} error="" onClose={vi.fn()} onSubmit={onSubmit} />)
    fireEvent.submit(screen.getByRole('button', { name: 'Salvar' }).closest('form')!)
    expect(onSubmit).toHaveBeenLastCalledWith(expect.objectContaining({ pixKeyType: null, pixKey: null }))
  })

  it('requires type and key together', () => {
    const onSubmit = vi.fn()
    render(<ManagerFormDialog open manager={{ ...manager, pixKey: null }} isSaving={false} error="" onClose={vi.fn()} onSubmit={onSubmit} />)
    fireEvent.submit(screen.getByRole('button', { name: 'Salvar' }).closest('form')!)
    expect(screen.getByText('Informe o tipo e a chave PIX juntos.')).toBeInTheDocument()
    expect(onSubmit).not.toHaveBeenCalled()
  })
})
