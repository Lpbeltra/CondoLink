import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { CondominiumMember } from '../management/types'

const managementApi = vi.hoisted(() => ({
  listCondominiumMembers: vi.fn(),
  listUnits: vi.fn(),
  onboardMember: vi.fn(),
  resetMemberTemporaryPassword: vi.fn(),
  updateCondominiumMember: vi.fn(),
}))

vi.mock('../management/api', () => managementApi)
vi.mock('../management/ManagementContext', () => ({
  useManagementContext: () => ({
    activeCondominiumId: 'condominium-id',
  }),
}))

import { ManagementPeoplePage } from './ManagementPeoplePage'

const member: CondominiumMember = {
  membershipId: 'membership-id',
  userId: 'user-id',
  fullName: 'Maria Silva',
  email: 'maria@example.com',
  phoneNumber: null,
  userActive: true,
  mustChangePassword: false,
  lastLoginAt: null,
  membershipActive: true,
  joinedAt: '2026-07-28T10:00:00Z',
  endedAt: null,
  roles: ['Resident'],
  unitLinks: [],
}

describe('ManagementPeoplePage password reset', () => {
  beforeEach(() => {
    managementApi.listCondominiumMembers.mockResolvedValue([member])
    managementApi.listUnits.mockResolvedValue([])
    managementApi.resetMemberTemporaryPassword.mockResolvedValue({
      userId: member.userId,
      fullName: member.fullName,
      email: member.email,
      temporaryPassword: 'NovaTemporaria1',
    })
    managementApi.updateCondominiumMember.mockResolvedValue({
      userId: member.userId,
      fullName: 'Maria Atualizada',
      email: member.email,
      phoneNumber: null,
      cpf: null,
      cnpj: null,
      address: null,
      city: null,
      state: null,
      membershipActive: true,
      unitLink: null,
    })
  })

  it('confirms reset and shows the new temporary credential once', async () => {
    const user = userEvent.setup()
    render(<ManagementPeoplePage />)

    await user.click(await screen.findByRole('button', {
      name: 'Redefinir senha temporária',
    }))
    expect(screen.getByText(
      'Redefinir senha temporária?',
    )).toBeInTheDocument()

    await user.click(screen.getByRole('button', {
      name: 'Gerar nova senha',
    }))

    expect(await screen.findByText(
      'Senha temporária regenerada.',
    )).toBeInTheDocument()
    expect(screen.getByText(/NovaTemporaria1/)).toBeInTheDocument()
    expect(managementApi.resetMemberTemporaryPassword).toHaveBeenCalledWith(
      'condominium-id',
      'user-id',
    )
    await waitFor(() => {
      expect(screen.getByText('Senha temporária')).toBeInTheDocument()
    })
  })

  it('opens the populated edit form and updates the list locally', async () => {
    const user = userEvent.setup()
    render(<ManagementPeoplePage />)

    await user.click(await screen.findByRole('button', { name: 'Editar' }))
    expect(screen.getByRole('heading', {
      name: 'Editar pessoa',
    })).toBeInTheDocument()
    const name = screen.getByRole('textbox', { name: 'Nome completo' })
    expect(name).toHaveValue('Maria Silva')

    await user.clear(name)
    await user.type(name, 'Maria Atualizada')
    await user.click(screen.getByRole('button', {
      name: 'Salvar alterações',
    }))

    expect(await screen.findByText(
      'Pessoa atualizada com sucesso.',
    )).toBeInTheDocument()
    expect(screen.getByText('Maria Atualizada')).toBeInTheDocument()
    expect(managementApi.updateCondominiumMember).toHaveBeenCalledWith(
      'condominium-id',
      'user-id',
      expect.objectContaining({ fullName: 'Maria Atualizada' }),
    )
  })
})
