import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

const api = vi.hoisted(() => ({
  listSubManagers: vi.fn(), listOverwatchCondominiums: vi.fn(), createSubManager: vi.fn(), updateSubManager: vi.fn(),
  setSubManagerStatus: vi.fn(), listSubManagerPermissions: vi.fn(), updateSubManagerPermissions: vi.fn(),
  resendSubManagerFirstAccess: vi.fn(), resetSubManagerPassword: vi.fn(),
}))
vi.mock('../submanagers/api', () => ({ ...api, subManagerModules: ['Requests', 'Attendance', 'ManagementCompany', 'Agenda', 'Assistant', 'Documents', 'Management'] }))
vi.mock('../condominiums/api', () => ({ listOverwatchCondominiums: api.listOverwatchCondominiums }))

import { OverwatchSubManagersPage } from './OverwatchSubManagersPageV2'

const s1 = { id: 's1', fullName: 'Tatiana', email: 's1@test.local', phoneNumber: '5511999990001', condominiumId: 'c1', condominiumName: 'Monticello', isActive: true, hasActiveLink: true, pixKeyType: 'Phone', pixKey: '5511999990001' }
const s2 = { ...s1, id: 's2', fullName: 'Maria', email: 's2@test.local', phoneNumber: '5511999990002', pixKey: null, pixKeyType: null }

describe('OverwatchSubManagersPage row isolation', () => {
  afterEach(() => cleanup())
  beforeEach(() => { vi.clearAllMocks(); api.listSubManagers.mockResolvedValue([s1, s2]); api.listOverwatchCondominiums.mockResolvedValue([{ id: 'c1', name: 'Monticello' }]); api.listSubManagerPermissions.mockResolvedValue([{ module: 'Requests', allowed: true }]); api.updateSubManager.mockResolvedValue(undefined); api.updateSubManagerPermissions.mockResolvedValue(undefined); api.setSubManagerStatus.mockResolvedValue(undefined); api.resendSubManagerFirstAccess.mockResolvedValue({}); api.resetSubManagerPassword.mockResolvedValue({ temporaryPassword: 'Temp123!' }) })

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
})
