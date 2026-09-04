import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

const api = vi.hoisted(() => ({
  listSubManagers: vi.fn(), listOverwatchCondominiums: vi.fn(), searchExistingUsers: vi.fn(), createSubManager: vi.fn(), updateSubManager: vi.fn(),
  setSubManagerStatus: vi.fn(), listSubManagerPermissions: vi.fn(), updateSubManagerPermissions: vi.fn(),
  resendSubManagerFirstAccess: vi.fn(), resetSubManagerPassword: vi.fn(), hardDeleteSubManager: vi.fn(), hardDeleteSubManagerEligibility: vi.fn(),
}))
vi.mock('../submanagers/api', () => ({ ...api, subManagerModules: ['Attendance', 'ManagementCompany', 'Agenda', 'Assistant', 'Documents', 'Management'] }))
vi.mock('../condominiums/api', () => ({ listOverwatchCondominiums: api.listOverwatchCondominiums }))

import { OverwatchSubManagersPage } from './OverwatchSubManagersPageV2'

const s1 = { id: 's1', fullName: 'Tatiana', email: 's1@test.local', phoneNumber: '5511999990001', condominiumId: '00000000-0000-4000-8000-000000000001', condominiumName: 'Monticello', isActive: true, hasActiveLink: true, pixKeyType: 'Phone', pixKey: '5511999990001' }
const s2 = { ...s1, id: 's2', fullName: 'Maria', email: 's2@test.local', phoneNumber: '5511999990002', pixKey: null, pixKeyType: null }

describe('OverwatchSubManagersPage row isolation', () => {
  afterEach(() => cleanup())
  beforeEach(() => { vi.clearAllMocks(); api.listSubManagers.mockResolvedValue([s1, s2]); api.listOverwatchCondominiums.mockResolvedValue([{ id: '00000000-0000-4000-8000-000000000001', name: 'Monticello' }]); api.searchExistingUsers.mockResolvedValue([{ userId: 'resident-1', fullName: 'Aline Souza', email: 'aline@test.local', phoneNumber: '5511999990009', pixKeyType: null, pixKey: null, links: [{ condominiumId: '00000000-0000-4000-8000-000000000001', condominiumName: 'Monticello', unit: '304' }], condominiumId: '00000000-0000-4000-8000-000000000001', condominiumName: 'Monticello', unit: '304' }]); api.listSubManagerPermissions.mockResolvedValue([{ module: 'Attendance', allowed: true }]); api.createSubManager.mockResolvedValue({ id: 'resident-1', email: 'aline@test.local', temporaryPassword: null }); api.updateSubManager.mockResolvedValue(undefined); api.updateSubManagerPermissions.mockResolvedValue(undefined); api.setSubManagerStatus.mockResolvedValue(undefined); api.resendSubManagerFirstAccess.mockResolvedValue({}); api.resetSubManagerPassword.mockResolvedValue({ temporaryPassword: 'Temp123!' }) })

  it('edits each row and keeps new form empty after modal transitions', async () => {
    const user = userEvent.setup(); render(<OverwatchSubManagersPage />); await screen.findByText('Tatiana')
    fireEvent.click(screen.getAllByRole('button', { name: 'Editar' })[0]); expect(await screen.findByText(/Editar subs/)).toBeVisible(); expect(screen.getByDisplayValue('s1@test.local')).toBeVisible(); await user.click(screen.getByRole('button', { name: 'Cancelar' })); await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())
    await user.click(screen.getAllByRole('button', { name: 'Editar' })[1]); expect(await screen.findByText(/Editar subs/)).toBeVisible(); expect(screen.getByDisplayValue('s2@test.local')).toBeVisible(); await user.click(screen.getByRole('button', { name: 'Cancelar' })); await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())
    await user.click(screen.getByRole('button', { name: /Novo.*sub/i })); expect(await screen.findByRole('dialog')).toHaveTextContent(/Novo subs/); expect(screen.queryByDisplayValue('s1@test.local')).toBeNull(); expect(screen.queryByDisplayValue('s2@test.local')).toBeNull()
  })

  it('sends permissions, resend, reset and status actions to the selected row', async () => {
    const user = userEvent.setup(); render(<OverwatchSubManagersPage />); await screen.findByText('Tatiana')
    await user.click(screen.getAllByRole('button', { name: /permiss/i })[1]); await screen.findByText(/Permiss.*acesso/); expect(api.listSubManagerPermissions).toHaveBeenCalledWith('s2'); await user.click(screen.getByRole('button', { name: 'Cancelar' })); await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())
    fireEvent.click(screen.getAllByRole('button', { name: /Mais/ })[0]); await screen.findByRole('menuitem', { name: /Reenviar primeiro acesso/i }); await user.click(screen.getByRole('menuitem', { name: /Reenviar primeiro acesso/i })); expect(api.resendSubManagerFirstAccess).toHaveBeenCalledWith(s1)
    await user.click(screen.getAllByRole('button', { name: /Mais/ })[1]); await user.click(screen.getByRole('menuitem', { name: /Redefinir senha/i })); await waitFor(() => expect(api.resetSubManagerPassword).toHaveBeenCalledWith(s2)); await user.click(screen.getByRole('button', { name: 'Concluir' })); await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())
    await user.click(screen.getAllByRole('button', { name: /Mais/ })[0]); await user.click(screen.getByRole('menuitem', { name: /Inativar/i })); expect(api.setSubManagerStatus).toHaveBeenCalledWith('s1', false)
  })

  it('hard deletes an eligible row only after the exact confirmation', async () => {
    const user = userEvent.setup(); api.hardDeleteSubManagerEligibility.mockResolvedValue({ canHardDelete: true, reason: null, canRemoveLinkOnly: false }); api.hardDeleteSubManager.mockResolvedValue(undefined); render(<OverwatchSubManagersPage />); await screen.findByText('Tatiana'); await user.click(screen.getAllByRole('button', { name: /Mais/ })[0]); await user.click(screen.getByRole('menuitem', { name: /Excluir permanentemente/i })); expect((await screen.findAllByText(/Tatiana/)).length).toBeGreaterThan(1); const button = screen.getByRole('button', { name: /Excluir permanentemente$/i }); expect(button).toBeDisabled(); await user.type(screen.getByRole('textbox'), 'EXCLUIR PERMANENTEMENTE'); expect(button).toBeEnabled(); await user.click(button); await waitFor(() => expect(api.hardDeleteSubManager).toHaveBeenCalledWith('s1')); expect(screen.queryByText('Tatiana')).toBeNull();
  })

  it('does not delete a row rejected by eligibility', async () => {
    const user = userEvent.setup(); api.hardDeleteSubManagerEligibility.mockResolvedValue({ canHardDelete: false, reason: 'Este subsíndico possui histórico.', canRemoveLinkOnly: false }); render(<OverwatchSubManagersPage />); await screen.findByText('Tatiana'); await user.click(screen.getAllByRole('button', { name: /Mais/ })[0]); await user.click(screen.getByRole('menuitem', { name: /Excluir permanentemente/i })); expect(await screen.findByText(/possui histórico/)).toBeVisible(); expect(api.hardDeleteSubManager).not.toHaveBeenCalled();
  })

  it('promotes selected existing user and sends UserId without first-access credentials', async () => {
    const user = userEvent.setup(); render(<OverwatchSubManagersPage />); await screen.findByText('Tatiana')
    await user.click(screen.getByRole('button', { name: /Novo.*sub/i })); await user.click(screen.getByRole('button', { name: 'Usuário existente' }))
    await user.click(screen.getByRole('combobox', { name: 'Condomínio' })); await user.click(screen.getByRole('option', { name: 'Monticello' })); const search = screen.getByRole('combobox', { name: /Buscar por nome/i }); await user.type(search, 'Aline'); await waitFor(() => expect(api.searchExistingUsers).toHaveBeenCalled()); await user.keyboard('{ArrowDown}{Enter}'); await user.click(screen.getByRole('button', { name: 'Cadastrar' }))
    await waitFor(() => expect(api.createSubManager).toHaveBeenCalledWith(expect.objectContaining({ existingUserId: 'resident-1', condominiumId: '00000000-0000-4000-8000-000000000001' })))
    expect(screen.queryByText('Credenciais temporárias')).toBeNull()
  })
})
