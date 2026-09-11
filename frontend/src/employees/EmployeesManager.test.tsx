import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { EmployeesManager } from './EmployeesManager'

const list = vi.fn()
const create = vi.fn()
const update = vi.fn()
const setStatus = vi.fn()
vi.mock('./api', () => ({
  listEmployees: (...args: unknown[]) => list(...args),
  createEmployee: (...args: unknown[]) => create(...args),
  updateEmployee: (...args: unknown[]) => update(...args),
  setEmployeeStatus: (...args: unknown[]) => setStatus(...args),
}))

const employee = {
  id: 'e1', condominiumId: 'c1', fullName: 'João Porteiro', jobTitle: 'Porteiro',
  phoneNumber: '+5511987654321', email: null, registrationNumber: 'MAT-1',
  admissionDate: null, isActive: true, createdAt: '', updatedAt: '',
}

describe('EmployeesManager', () => {
  beforeEach(() => { list.mockReset(); create.mockReset(); update.mockReset(); setStatus.mockReset() })

  it('shows loading then the empty state', async () => {
    list.mockResolvedValue([])
    render(<EmployeesManager condominiumId="c1" />)
    expect(screen.getByText('Carregando funcionários…')).toBeInTheDocument()
    expect(await screen.findByText('Nenhum funcionário encontrado.')).toBeInTheDocument()
  })

  it('lists employees with contact chips and status', async () => {
    list.mockResolvedValue([employee])
    render(<EmployeesManager condominiumId="c1" />)
    expect(await screen.findByText('João Porteiro')).toBeInTheDocument()
    expect(screen.getByText('Porteiro')).toBeInTheDocument()
    expect(screen.getByText('+5511987654321')).toBeInTheDocument()
    expect(screen.getByText('Ativo')).toBeInTheDocument()
  })

  it('shows an error when the list fails to load', async () => {
    list.mockRejectedValue(new Error('boom'))
    render(<EmployeesManager condominiumId="c1" />)
    expect(await screen.findByText('Não foi possível carregar os funcionários.')).toBeInTheDocument()
  })

  it('filters by search term', async () => {
    list.mockResolvedValue([employee])
    render(<EmployeesManager condominiumId="c1" />)
    await screen.findByText('João Porteiro')
    const user = userEvent.setup()
    await user.type(screen.getByLabelText('Buscar'), 'zelador')
    await waitFor(() => expect(list).toHaveBeenLastCalledWith('c1', { search: 'zelador', status: 'active' }))
  })

  it('creates a new employee', async () => {
    list.mockResolvedValue([])
    create.mockResolvedValue({ ...employee, id: 'e2', fullName: 'Maria Zeladora' })
    render(<EmployeesManager condominiumId="c1" />)
    await screen.findByText('Nenhum funcionário encontrado.')
    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: 'Novo funcionário' }))
    await user.type(screen.getByLabelText(/Nome completo/), 'Maria Zeladora')
    await user.click(screen.getByRole('button', { name: 'Salvar' }))
    await waitFor(() => expect(create).toHaveBeenCalledWith('c1', expect.objectContaining({ fullName: 'Maria Zeladora' })))
    expect(await screen.findByText('Maria Zeladora')).toBeInTheDocument()
  })

  it('edits an existing employee and toggles status in the same save', async () => {
    list.mockResolvedValue([employee])
    update.mockResolvedValue({ ...employee, fullName: 'João Porteiro Silva' })
    setStatus.mockResolvedValue({ ...employee, fullName: 'João Porteiro Silva', isActive: false })
    render(<EmployeesManager condominiumId="c1" />)
    await screen.findByText('João Porteiro')
    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: 'Editar' }))
    const nameField = screen.getByLabelText(/Nome completo/)
    await user.clear(nameField)
    await user.type(nameField, 'João Porteiro Silva')
    await user.click(screen.getByLabelText('Ativo'))
    await user.click(screen.getByRole('button', { name: 'Salvar' }))
    await waitFor(() => expect(update).toHaveBeenCalledWith('c1', 'e1', expect.objectContaining({ fullName: 'João Porteiro Silva' })))
    await waitFor(() => expect(setStatus).toHaveBeenCalledWith('c1', 'e1', false))
  })

  it('toggles status directly from the list', async () => {
    list.mockResolvedValue([employee])
    setStatus.mockResolvedValue({ ...employee, isActive: false })
    render(<EmployeesManager condominiumId="c1" />)
    await screen.findByText('João Porteiro')
    await userEvent.setup().click(screen.getByRole('button', { name: 'Inativar' }))
    await waitFor(() => expect(setStatus).toHaveBeenCalledWith('c1', 'e1', false))
  })
})
