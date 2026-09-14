import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { CondominiumModulesCard } from './CondominiumModulesCard'

const { get, put } = vi.hoisted(() => ({ get: vi.fn(), put: vi.fn() }))
vi.mock('../../services/api', () => ({ api: { get, put } }))
const modules = () => [
  { module: 'Assistant', enabled: true, managementCompanyAccessEnabled: false, supportsManagementCompanyAccess: false },
  { module: 'EmployeeManagement', enabled: true, managementCompanyAccessEnabled: true, supportsManagementCompanyAccess: true },
]
describe('CondominiumModulesCard', () => {
  beforeEach(() => { get.mockReset(); put.mockReset() })
  it('hides Employee Management from condominium modules', async () => {
    get.mockResolvedValue({ data: modules() }); render(<CondominiumModulesCard condominiumId="A" />)
    expect(await screen.findByRole('switch', { name: 'Assistente' })).toBeChecked()
    expect(screen.queryByRole('switch', { name: 'EmployeeManagement' })).not.toBeInTheDocument()
    expect(screen.queryByText('Permitir operação pela administradora')).not.toBeInTheDocument()
  })
  it('preserves operational module save behavior', async () => {
    get.mockResolvedValue({ data: modules() }); put.mockResolvedValue({ data: {} }); const user = userEvent.setup()
    render(<CondominiumModulesCard condominiumId="A" />); await user.click(await screen.findByRole('switch', { name: 'Assistente' }))
    expect(put).toHaveBeenCalledWith('/overwatch/condominiums/A/modules', expect.anything())
  })
  it('reloads when condominium changes', async () => {
    get.mockResolvedValue({ data: modules() }); const view = render(<CondominiumModulesCard condominiumId="A" />)
    await screen.findByRole('switch', { name: 'Assistente' }); view.rerender(<CondominiumModulesCard condominiumId="B" />)
    await waitFor(() => expect(get).toHaveBeenLastCalledWith('/overwatch/condominiums/B/modules'))
  })
})
