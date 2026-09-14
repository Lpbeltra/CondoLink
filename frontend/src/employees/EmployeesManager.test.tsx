import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi, beforeEach } from 'vitest'
import { EmployeesManager } from './EmployeesManager'
import type { Employee } from './api'

const api = vi.hoisted(() => ({
  listEmployees: vi.fn(),
  getEmployee: vi.fn(),
  createEmployee: vi.fn(),
  updateEmployee: vi.fn(),
  setEmployeeStatus: vi.fn(),
  listEmployeeManagementCondominiums: vi.fn(),
}))

vi.mock('./api', async () => {
  const actual = await vi.importActual<typeof import('./api')>('./api')
  return {
    ...actual,
    listEmployees: api.listEmployees,
    getEmployee: api.getEmployee,
    createEmployee: api.createEmployee,
    updateEmployee: api.updateEmployee,
    setEmployeeStatus: api.setEmployeeStatus,
  }
})
vi.mock('../administrator/employeeManagement/api', () => ({
  listEmployeeManagementCondominiums: api.listEmployeeManagementCondominiums,
}))

const employee: Employee = {
  id: 'employee-1', condominiumId: 'condo-1', fullName: 'Ana Empregada', cpf: '***.***.***-25',
  jobTitle: 'Zeladora', phoneNumber: '11999990000', email: 'ana@example.com', registrationNumber: 'MAT-1',
  admissionDate: null, isActive: true, createdAt: '2026-01-01T00:00:00Z', updatedAt: '2026-01-01T00:00:00Z',
}

beforeEach(() => {
  vi.clearAllMocks()
  api.listEmployeeManagementCondominiums.mockResolvedValue([{ id: 'condo-1', name: 'Monticello' }])
})

describe('EmployeesManager listing', () => {
  it('renders no selection checkboxes anywhere on the page', async () => {
    api.listEmployees.mockResolvedValue([employee])
    render(<EmployeesManager />)
    await screen.findByText('Ana Empregada')
    expect(screen.queryAllByRole('checkbox', { name: /selecionar/i })).toHaveLength(0)
    expect(screen.queryByText(/selecionado\(s\)/)).not.toBeInTheDocument()
    expect(screen.queryByText('Selecionar todos filtrados')).not.toBeInTheDocument()
    // The only checkbox on the page is the "Ocultar CPF" filter toggle.
    expect(screen.getAllByRole('checkbox')).toHaveLength(1)
  })

  it('shows only Condomínio, CPF, Telefone and Situação as tags — never matrícula or e-mail', async () => {
    api.listEmployees.mockResolvedValue([employee])
    render(<EmployeesManager />)
    await screen.findByText('Ana Empregada')
    expect(screen.getByText('Monticello')).toBeInTheDocument()
    expect(screen.getByText('CPF ***.***.***-25')).toBeInTheDocument()
    expect(screen.getByText('11999990000')).toBeInTheDocument()
    expect(screen.getByText('Ativo')).toBeInTheDocument()
    expect(screen.queryByText(/MAT-1/)).not.toBeInTheDocument()
    expect(screen.queryByText(/ana@example\.com/)).not.toBeInTheDocument()
  })

  it('masks CPF by default and reveals it when "Ocultar CPF" is unchecked', async () => {
    api.listEmployees.mockResolvedValue([employee])
    render(<EmployeesManager />)
    await screen.findByText('CPF ***.***.***-25')
    expect(api.listEmployees).toHaveBeenLastCalledWith(expect.objectContaining({ revealCpf: false }))

    api.listEmployees.mockResolvedValue([{ ...employee, cpf: '529.982.247-25' }])
    await userEvent.click(screen.getByRole('checkbox', { name: 'Ocultar CPF' }))

    await screen.findByText('CPF 529.982.247-25')
    expect(api.listEmployees).toHaveBeenLastCalledWith(expect.objectContaining({ revealCpf: true }))
  })
})

describe('EmployeesManager activate/inactivate regression', () => {
  it('keeps the card fully populated through inactivate then reactivate — never blank', async () => {
    api.listEmployees.mockResolvedValue([employee])
    render(<EmployeesManager />)
    await screen.findByText('Ana Empregada')

    const card = screen.getByText('Ana Empregada').closest('.MuiCard-root') as HTMLElement
    expect(within(card).getByText('Inativar')).toBeInTheDocument()

    // Backend returns the FULL employee, not a partial payload.
    api.setEmployeeStatus.mockResolvedValue({ ...employee, isActive: false })
    await userEvent.click(within(card).getByText('Inativar'))

    await waitFor(() => expect(within(card).getByText('Inativo')).toBeInTheDocument())
    expect(within(card).getByText('Ana Empregada')).toBeInTheDocument()
    expect(within(card).getByText('Zeladora')).toBeInTheDocument()
    expect(within(card).getByText('Monticello')).toBeInTheDocument()
    expect(within(card).getByText('CPF ***.***.***-25')).toBeInTheDocument()
    expect(within(card).getByText('11999990000')).toBeInTheDocument()
    expect(within(card).getByText('Ativar')).toBeInTheDocument()

    api.setEmployeeStatus.mockResolvedValue({ ...employee, isActive: true })
    await userEvent.click(within(card).getByText('Ativar'))

    await waitFor(() => expect(within(card).getByText('Ativo')).toBeInTheDocument())
    expect(within(card).getByText('Ana Empregada')).toBeInTheDocument()
    expect(within(card).getByText('Zeladora')).toBeInTheDocument()
    expect(within(card).getByText('Monticello')).toBeInTheDocument()
    expect(within(card).getByText('CPF ***.***.***-25')).toBeInTheDocument()
    expect(within(card).getByText('Inativar')).toBeInTheDocument()
  })
})

describe('EmployeesManager form', () => {
  it('has no "Ativo" checkbox — status only changes via Ativar/Inativar', async () => {
    api.listEmployees.mockResolvedValue([employee])
    render(<EmployeesManager />)
    await userEvent.click(await screen.findByText('Novo funcionário'))
    const dialog = (await screen.findByText('Novo funcionário', { selector: '.MuiDialogTitle-root' })).closest('.MuiDialog-root') as HTMLElement
    expect(screen.queryByLabelText('Ativo')).not.toBeInTheDocument()
    expect(within(dialog).queryByText('Ativo')).not.toBeInTheDocument()
  })

  it('fetches and shows the full CPF when editing, never the masked list value', async () => {
    api.listEmployees.mockResolvedValue([employee])
    api.getEmployee.mockResolvedValue({ ...employee, cpf: '529.982.247-25' })
    render(<EmployeesManager />)
    await userEvent.click(await screen.findByText('Editar'))

    await waitFor(() => expect(api.getEmployee).toHaveBeenCalledWith('employee-1'))
    await waitFor(() => expect(screen.getByLabelText('CPF')).toHaveValue('529.982.247-25'))
  })
})
