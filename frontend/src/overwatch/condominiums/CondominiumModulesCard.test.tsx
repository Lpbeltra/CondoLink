import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { CondominiumModulesCard } from './CondominiumModulesCard'

const { get, put } = vi.hoisted(() => ({ get: vi.fn(), put: vi.fn() }))
vi.mock('../../services/api', () => ({ api: { get, put } }))

const modules = (employeeEnabled = true) => [
  { module: 'Assistant', enabled: true, managementCompanyAccessEnabled: false, supportsManagementCompanyAccess: false },
  { module: 'EmployeeManagement', enabled: employeeEnabled, managementCompanyAccessEnabled: employeeEnabled, supportsManagementCompanyAccess: true }
]

describe('CondominiumModulesCard', () => {
  beforeEach(() => { get.mockReset(); put.mockReset() })

  it('loads requested condominium, reflects state, and only shows supported delegation', async () => {
    get.mockResolvedValue({ data: modules() })
    render(<CondominiumModulesCard condominiumId="A" />)
    expect(await screen.findByRole('switch', { name: 'Assistente' })).toBeChecked()
    expect(screen.getByRole('switch', { name: 'Gestão de Funcionários' })).toBeChecked()
    expect(screen.getByRole('switch', { name: 'Permitir operação pela administradora' })).toBeChecked()
    expect(screen.queryAllByRole('switch', { name: 'Permitir operação pela administradora' })).toHaveLength(1)
    expect(get).toHaveBeenCalledWith('/overwatch/condominiums/A/modules')
  })

  it('saves once while loading and restores state with feedback on failure', async () => {
    get.mockResolvedValue({ data: modules(false) })
    let rejectPut!: () => void
    put.mockImplementation(() => new Promise<void>((_, reject) => { rejectPut = reject }))
    const user = userEvent.setup()
    render(<CondominiumModulesCard condominiumId="A" />)
    const employee = await screen.findByRole('switch', { name: 'Gestão de Funcionários' })
    expect(screen.getByRole('switch', { name: 'Permitir operação pela administradora' })).toBeDisabled()
    await user.click(employee)
    expect(put).toHaveBeenCalledTimes(1)
    expect(employee).toBeDisabled()
    rejectPut()
    await waitFor(() => expect(screen.getByText('Não foi possível salvar o módulo.')).toBeVisible())
    expect(screen.getByRole('switch', { name: 'Gestão de Funcionários' })).not.toBeChecked()
  })

  it('clears prior state and reloads when condominium changes', async () => {
    get.mockImplementation((url: string) => Promise.resolve({ data: url.includes('/A/') ? modules() : modules(false) }))
    const view = render(<CondominiumModulesCard condominiumId="A" />)
    expect(await screen.findByRole('switch', { name: 'Gestão de Funcionários' })).toBeChecked()
    view.rerender(<CondominiumModulesCard condominiumId="B" />)
    await waitFor(() => expect(screen.getByRole('switch', { name: 'Gestão de Funcionários' })).not.toBeChecked())
    expect(get).toHaveBeenLastCalledWith('/overwatch/condominiums/B/modules')
  })
})
