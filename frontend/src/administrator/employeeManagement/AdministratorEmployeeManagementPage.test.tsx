import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { AdministratorEmployeeManagementPage } from './AdministratorEmployeeManagementPage'

const list = vi.fn()
vi.mock('./api', () => ({ listDelegatedCondominiums: () => list() }))
vi.mock('../../employees/EmployeesManager', () => ({
  EmployeesManager: ({ condominiumId }: { condominiumId: string }) => <div>Manager for {condominiumId}</div>,
}))

describe('AdministratorEmployeeManagementPage', () => {
  beforeEach(() => list.mockReset())

  it('shows an info message when no condominium delegates the module', async () => {
    list.mockResolvedValue([])
    render(<AdministratorEmployeeManagementPage />)
    expect(await screen.findByText('Nenhum condomínio delegou a Gestão de Funcionários para sua administradora.')).toBeInTheDocument()
  })

  it('auto-selects the single delegated condominium', async () => {
    list.mockResolvedValue([{ condominiumId: 'c1', name: 'Condomínio A' }])
    render(<AdministratorEmployeeManagementPage />)
    expect(await screen.findByText('Manager for c1')).toBeInTheDocument()
  })

  it('lets the user pick among multiple delegated condominiums', async () => {
    list.mockResolvedValue([
      { condominiumId: 'c1', name: 'Condomínio A' },
      { condominiumId: 'c2', name: 'Condomínio B' },
    ])
    render(<AdministratorEmployeeManagementPage />)
    await screen.findByLabelText('Condomínio')
    expect(screen.queryByText('Manager for c1')).not.toBeInTheDocument()
    const user = userEvent.setup()
    await user.click(screen.getByLabelText('Condomínio'))
    await user.click(await screen.findByRole('option', { name: 'Condomínio B' }))
    await waitFor(() => expect(screen.getByText('Manager for c2')).toBeInTheDocument())
  })

  it('shows an error when the condominium list fails to load', async () => {
    list.mockRejectedValue(new Error('boom'))
    render(<AdministratorEmployeeManagementPage />)
    expect(await screen.findByText('Não foi possível carregar os condomínios.')).toBeInTheDocument()
  })
})
