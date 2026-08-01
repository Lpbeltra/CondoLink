import { fireEvent, render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { CondominiumFormDialog } from './CondominiumFormDialog'
import type { OverwatchCondominium } from './types'

const baseProps = {
  open: true,
  managementCompanies: [],
  isSaving: false,
  error: '',
  onClose: vi.fn(),
  onSubmit: vi.fn().mockResolvedValue(undefined),
}

describe('CondominiumFormDialog WhatsApp setting', () => {
  it('shows the switch enabled by default with its explanatory message', () => {
    render(<CondominiumFormDialog {...baseProps} condominium={null} />)

    expect(screen.getByRole('switch', { name: 'Atualizações pelo WhatsApp' }))
      .toBeChecked()
    expect(screen.getByText(
      'Permite enviar aos moradores atualizações de status e solicitações de resposta pelo WhatsApp.',
    )).toBeInTheDocument()
    expect(screen.getByRole('checkbox', { name: 'Possui portaria' }))
      .not.toBeChecked()
  })

  it('loads the persisted value on edit and allows changing it', () => {
    const condominium: OverwatchCondominium = {
      id: 'id', name: 'Residencial', email: null, cnpj: '04252011000110',
      address: 'Rua A', city: 'São Paulo', state: 'SP', hasDoorman: true,
      isRemoteDoorman: false, doormanContact: null, whatsAppUpdatesEnabled: false,
      isActive: true, createdAt: '2026-01-01T00:00:00Z',
      updatedAt: '2026-01-01T00:00:00Z', managementCompanyId: null,
      managementCompanyName: null, managerCount: 0,
    }
    render(<CondominiumFormDialog {...baseProps} condominium={condominium} />)
    const whatsapp = screen.getByRole('switch', {
      name: 'Atualizações pelo WhatsApp',
    })

    expect(whatsapp).not.toBeChecked()
    fireEvent.click(whatsapp)
    expect(whatsapp).toBeChecked()
    expect(screen.getByRole('checkbox', { name: 'Possui portaria' })).toBeChecked()
  })
})
