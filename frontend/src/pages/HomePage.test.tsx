import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { HomePage } from './HomePage'

const state = vi.hoisted(() => ({
  mobile: true,
  user: { fullName: 'Maria Silva', roles: ['Manager'] },
  management: {
    activeCondominium: { id: 'condo', name: 'Aurora' } as { id: string; name: string } | null,
    condominiumCount: 1, isLoading: false,
    managementRoles: ['Manager'], subManagerPermissions: [] as string[],
  },
  resident: false,
  administrator: null as object | null,
}))
vi.mock('@mui/material', async original => ({ ...await original<typeof import('@mui/material')>(), useMediaQuery: () => state.mobile }))
vi.mock('../auth/AuthContext', () => ({ useAuth: () => ({ user: state.user }) }))
vi.mock('../management/ManagementContext', () => ({ useManagementContext: () => state.management }))
vi.mock('../condominiums/CondominiumContext', () => ({ useCondominium: () => ({ currentCondominium: null, isResident: state.resident }) }))
vi.mock('../administrator/AdministratorContext', () => ({ useAdministrator: () => ({ value: state.administrator, loading: false }) }))
vi.mock('../management/components/ManagementCondominiumSwitcher', () => ({ ManagementCondominiumSwitcher: () => null }))

function page(path = '/app') {
  return render(<MemoryRouter initialEntries={[path]}><Routes>
    <Route path="/app" element={<HomePage />} />
    <Route path="/management/dashboard" element={<div>Dashboard destination</div>} />
    <Route path="/management/requests" element={<div>Attendance destination</div>} />
    <Route path="/management/requests/:id" element={<div>Deep link destination</div>} />
    <Route path="/administrator/requests" element={<div>Administrator destination</div>} />
  </Routes></MemoryRouter>)
}

describe('home entry by viewport and resolved context', () => {
  beforeEach(() => {
    state.mobile = true
    state.user.roles = ['Manager']
    state.management = { activeCondominium: { id: 'condo', name: 'Aurora' }, condominiumCount: 1, isLoading: false, managementRoles: ['Manager'], subManagerPermissions: [] }
    state.resident = false
    state.administrator = null
  })
  it('opens attendance for a mobile manager', () => { page(); expect(screen.getByText('Attendance destination')).toBeVisible() })
  it('keeps the desktop dashboard', () => { state.mobile = false; page(); expect(screen.getByText('Dashboard destination')).toBeVisible() })
  it('waits for management context', () => { state.management.isLoading = true; page(); expect(screen.queryByText('Attendance destination')).not.toBeInTheDocument() })
  it('keeps all condominiums selected', () => { state.management.activeCondominium = null; state.management.condominiumCount = 2; page(); expect(screen.getByText('Attendance destination')).toBeVisible(); expect(state.management.activeCondominium).toBeNull() })
  it.each([true, false])('respects SubManager Attendance permission (%s)', allowed => {
    state.management.managementRoles = ['SubManager']; state.user.roles = ['SubManager']
    state.management.subManagerPermissions = allowed ? ['Attendance'] : ['Management']
    page(); expect(screen.getByText(allowed ? 'Attendance destination' : 'Dashboard destination')).toBeVisible()
  })
  it('leaves PlatformAdmin entry unchanged', () => { state.user.roles = ['PlatformAdmin']; page(); expect(screen.getByRole('heading', { name: 'Olá, Maria' })).toBeVisible() })
  it('keeps the administrator portal entry', () => { state.management.condominiumCount = 0; state.management.activeCondominium = null; state.administrator = {}; page(); expect(screen.getByText('Administrator destination')).toBeVisible() })
  it('leaves residents on their existing home', () => { state.management.condominiumCount = 0; state.management.activeCondominium = null; state.resident = true; state.user.roles = ['Resident']; page(); expect(screen.getByRole('button', { name: 'Ver minhas solicitações' })).toBeVisible() })
  it('does not intercept a deep link', () => { page('/management/requests/request?tab=history'); expect(screen.getByText('Deep link destination')).toBeVisible() })
})
