import { cleanup, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

const api = vi.hoisted(() => ({
  listManagementCompanyEmployees: vi.fn(),
  hardDeleteManagementCompanyEmployeeEligibility: vi.fn(),
  hardDeleteManagementCompanyEmployee: vi.fn(),
  createManagementCompanyEmployee: vi.fn(),
  removeManagementCompanyEmployee: vi.fn(),
  updateManagementCompanyEmployeeStatus: vi.fn(),
  resendManagementCompanyAccess: vi.fn(),
  resetManagementCompanyAccessPassword: vi.fn(),
}))

vi.mock('./api', () => api)

import { ManagementCompanyEmployees } from './ManagementCompanyEmployees'

const employee = {
  id: 'access-1', managementCompanyId: 'company-1', userId: 'user-1',
  fullName: 'Funcionário Teste', email: 'funcionario@test.local', contact: null,
  jobTitle: 'Operador', accessType: 'Person' as const, isActive: true,
  lastAccessAt: null, categoryIds: [], createdAt: '2026-01-01', updatedAt: '2026-01-01',
}

describe('ManagementCompanyEmployees hard delete', () => {
  afterEach(() => cleanup())
  beforeEach(() => {
    vi.clearAllMocks()
    api.listManagementCompanyEmployees.mockResolvedValue([employee])
    api.hardDeleteManagementCompanyEmployeeEligibility.mockResolvedValue({ canHardDelete: true, reason: null })
    api.hardDeleteManagementCompanyEmployee.mockResolvedValue(undefined)
    api.resendManagementCompanyAccess.mockResolvedValue({ sent: true })
    api.resetManagementCompanyAccessPassword.mockResolvedValue({ temporaryPassword: 'Temp123!', invitationSent: true })
  })

  async function openDelete(user: ReturnType<typeof userEvent.setup>) {
    await screen.findByText(employee.fullName)
    await user.click(screen.getByRole('button', { name: `Mais ações de ${employee.fullName}` }))
    await user.click(screen.getByRole('menuitem', { name: 'Excluir permanentemente' }))
  }

  it('uses eligibility, exact confirmation, delete, refresh and success feedback', async () => {
    const user = userEvent.setup()
    render(<ManagementCompanyEmployees managementCompanyId="company-1" />)
    await openDelete(user)
    expect(api.hardDeleteManagementCompanyEmployeeEligibility).toHaveBeenCalledWith(employee.id)
    expect(await screen.findByRole('dialog')).toHaveTextContent(employee.fullName)
    const submit = screen.getByRole('button', { name: /Excluir permanentemente$/i })
    expect(submit).toBeDisabled()
    await user.type(screen.getByRole('textbox'), 'EXCLUIR')
    expect(submit).toBeDisabled()
    await user.clear(screen.getByRole('textbox'))
    await user.type(screen.getByRole('textbox'), 'EXCLUIR PERMANENTEMENTE')
    expect(submit).toBeEnabled()
    await user.click(submit)
    await waitFor(() => expect(api.hardDeleteManagementCompanyEmployee).toHaveBeenCalledWith(employee.id))
    expect(screen.queryByText(employee.fullName)).toBeNull()
    expect(await screen.findByText('Acesso excluído permanentemente.')).toBeVisible()
  })

  it('shows ineligible reason and never deletes', async () => {
    const user = userEvent.setup()
    api.hardDeleteManagementCompanyEmployeeEligibility.mockResolvedValue({
      canHardDelete: false, reason: 'Este acesso possui histórico e precisa permanecer.',
    })
    render(<ManagementCompanyEmployees managementCompanyId="company-1" />)
    await openDelete(user)
    expect(await screen.findByText(/possui histórico/)).toBeVisible()
    expect(api.hardDeleteManagementCompanyEmployee).not.toHaveBeenCalled()
  })

  it('keeps row and shows backend reason on conflict', async () => {
    const user = userEvent.setup()
    api.hardDeleteManagementCompanyEmployee.mockRejectedValue({
      response: { status: 409, data: { message: 'Este acesso mudou e permanece no histórico.' } },
      isAxiosError: true,
      config: {},
    })
    render(<ManagementCompanyEmployees managementCompanyId="company-1" />)
    await openDelete(user)
    const input = screen.getByRole('textbox')
    await user.type(input, 'EXCLUIR PERMANENTEMENTE')
    await user.click(screen.getByRole('button', { name: /Excluir permanentemente$/i }))
    expect(await screen.findByText('Este acesso mudou e permanece no histórico.')).toBeVisible()
    expect(screen.getAllByText(employee.fullName)[0]).toBeVisible()
  })

  it('keeps row and reports unexpected delete errors without duplicating calls', async () => {
    const user = userEvent.setup()
    api.hardDeleteManagementCompanyEmployee.mockRejectedValue(new Error('network'))
    render(<ManagementCompanyEmployees managementCompanyId="company-1" />)
    await openDelete(user)
    await user.type(screen.getByRole('textbox'), 'EXCLUIR PERMANENTEMENTE')
    await user.click(screen.getByRole('button', { name: /Excluir permanentemente$/i }))
    expect((await screen.findAllByRole('alert')).at(-1)).toBeVisible()
    expect(api.hardDeleteManagementCompanyEmployee).toHaveBeenCalledTimes(1)
    expect(screen.getAllByText(employee.fullName)[0]).toBeVisible()
  })
})
