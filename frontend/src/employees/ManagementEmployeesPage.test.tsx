import { render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { ManagementEmployeesPage } from './ManagementEmployeesPage'
import { useManagementContext } from '../management/ManagementContext'
import { useCondominiumModules } from '../modules/useCondominiumModules'

vi.mock('../management/ManagementContext', () => ({ useManagementContext: vi.fn() }))
vi.mock('../modules/useCondominiumModules', () => ({ useCondominiumModules: vi.fn() }))
vi.mock('./EmployeesManager', () => ({
  EmployeesManager: ({ condominiumId }: { condominiumId: string }) => <div>Manager for {condominiumId}</div>,
}))

describe('ManagementEmployeesPage', () => {
  it('prompts to select a condominium when none is active', () => {
    vi.mocked(useManagementContext).mockReturnValue({ activeCondominiumId: null } as never)
    vi.mocked(useCondominiumModules).mockReturnValue({ isModuleEnabled: () => false, loading: false } as never)
    render(<ManagementEmployeesPage />)
    expect(screen.getByText('Selecione um condomínio.')).toBeInTheDocument()
  })

  it('shows ModuleUnavailable when the module is disabled', () => {
    vi.mocked(useManagementContext).mockReturnValue({ activeCondominiumId: 'c1' } as never)
    vi.mocked(useCondominiumModules).mockReturnValue({ isModuleEnabled: () => false, loading: false } as never)
    render(<ManagementEmployeesPage />)
    expect(screen.getByText('Este recurso não está disponível para este condomínio.')).toBeInTheDocument()
  })

  it('renders the employees manager when the module is enabled', () => {
    vi.mocked(useManagementContext).mockReturnValue({ activeCondominiumId: 'c1' } as never)
    vi.mocked(useCondominiumModules).mockReturnValue({
      isModuleEnabled: (module: string) => module === 'EmployeeManagement', loading: false,
    } as never)
    render(<ManagementEmployeesPage />)
    expect(screen.getByText('Manager for c1')).toBeInTheDocument()
  })
})
