import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'

const mocks = vi.hoisted(() => ({
  getManagementContext: vi.fn(),
  setManagementContext: vi.fn(),
  user: { id: 'manager-1', fullName: 'Manager' },
}))

vi.mock('../auth/AuthContext', () => ({ useAuth: () => ({ user: mocks.user }) }))
vi.mock('./api', () => ({
  getManagementContext: mocks.getManagementContext,
  setManagementContext: mocks.setManagementContext,
}))

import { useManagementContext } from './ManagementContext'
import { ManagementContextProvider } from './ManagementContextProvider'
import type { ManagementContextResponse } from './types'

const condominiums = [
  { id: 'condo-1', name: 'Aurora', isActive: true },
  { id: 'condo-2', name: 'Bosque', isActive: true },
]

function context(activeId: string | null, consolidated: boolean): ManagementContextResponse {
  return {
    activeManagementCondominiumId: activeId,
    activeCondominium: condominiums.find(item => item.id === activeId) ?? null,
    availableCondominiums: condominiums,
    condominiumCount: condominiums.length,
    usesConsolidatedManagementScope: consolidated,
    hasEligibleManagementCompany: true,
    managementRoles: ['Manager'],
    subManagerPermissions: ['Attendance'],
  }
}

function Probe() {
  const value = useManagementContext()
  return <>
    <output aria-label="condomínio ativo">{value.activeCondominiumId ?? 'todos'}</output>
    <output aria-label="escopo consolidado">{String(value.usesConsolidatedManagementScope)}</output>
    <output aria-label="permissões">{value.subManagerPermissions?.join(',') ?? 'nenhuma'}</output>
    <output aria-label="troca em andamento">{String(value.isSwitching)}</output>
    {value.error && <div role="alert">{value.error}</div>}
    <button onClick={() => void value.selectCondominium(null)}>Todos</button>
    <button onClick={() => void value.selectCondominium('condo-2')}>Bosque</button>
  </>
}

function renderProvider() {
  return render(<ManagementContextProvider><Probe /></ManagementContextProvider>)
}

describe('ManagementContextProvider switching baseline', () => {
  beforeEach(() => {
    mocks.getManagementContext.mockReset().mockResolvedValue(context('condo-1', false))
    mocks.setManagementContext.mockReset()
  })

  it('selects Todos as a real null context and adopts the server scope', async () => {
    mocks.setManagementContext.mockResolvedValue(context(null, true))
    const user = userEvent.setup()
    renderProvider()
    expect(await screen.findByLabelText('condomínio ativo')).toHaveTextContent('condo-1')

    await user.click(screen.getByRole('button', { name: 'Todos' }))

    await waitFor(() => expect(mocks.setManagementContext).toHaveBeenCalledWith(null))
    expect(screen.getByLabelText('condomínio ativo')).toHaveTextContent('todos')
    expect(screen.getByLabelText('escopo consolidado')).toHaveTextContent('true')
    expect(screen.getByLabelText('permissões')).toHaveTextContent('Attendance')
    expect(screen.getByLabelText('troca em andamento')).toHaveTextContent('false')
  })

  it('restores condominium, scope and permissions when switching fails', async () => {
    mocks.setManagementContext.mockRejectedValue(new Error('network'))
    const user = userEvent.setup()
    renderProvider()
    expect(await screen.findByLabelText('condomínio ativo')).toHaveTextContent('condo-1')

    await user.click(screen.getByRole('button', { name: 'Bosque' }))

    expect(await screen.findByRole('alert')).toBeVisible()
    expect(mocks.setManagementContext).toHaveBeenCalledWith('condo-2')
    expect(screen.getByLabelText('condomínio ativo')).toHaveTextContent('condo-1')
    expect(screen.getByLabelText('escopo consolidado')).toHaveTextContent('false')
    expect(screen.getByLabelText('permissões')).toHaveTextContent('Attendance')
    expect(screen.getByLabelText('troca em andamento')).toHaveTextContent('false')
  })
})
