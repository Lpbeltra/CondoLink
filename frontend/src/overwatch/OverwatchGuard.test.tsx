import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { OverwatchGuard } from './OverwatchGuard'

const state = vi.hoisted(() => ({
  mobile: true,
  isInitializing: false,
  user: { roles: ['PlatformAdmin', 'Manager'] as string[] },
  management: { isLoading: false, isSwitching: false, condominiumCount: 1, managementRoles: ['Manager'] as string[], subManagerPermissions: [] as string[] },
  currentCondominium: null as { roles: string[] } | null,
}))

vi.mock('../auth/AuthContext', () => ({ useAuth: () => ({ user: state.user, isInitializing: state.isInitializing }) }))
vi.mock('../management/ManagementContext', () => ({ useManagementContext: () => state.management }))
vi.mock('../condominiums/CondominiumContext', () => ({ useCondominium: () => ({ currentCondominium: state.currentCondominium }) }))

function renderRoute(path: string) {
  return render(<MemoryRouter initialEntries={[path]}><Routes>
    <Route element={<OverwatchGuard />}>
      <Route path="/overwatch/*" element={<div>Overwatch page</div>} />
    </Route>
    <Route path="/management/requests" element={<div>Attendance page</div>} />
    <Route path="/" element={<div>Home</div>} />
  </Routes></MemoryRouter>)
}

describe('mobile Overwatch route guard', () => {
  beforeEach(() => {
    state.mobile = true
    state.isInitializing = false
    state.user.roles = ['PlatformAdmin', 'Manager']
    state.management = { isLoading: false, isSwitching: false, condominiumCount: 1, managementRoles: ['Manager'], subManagerPermissions: [] }
    state.currentCondominium = null
    vi.spyOn(window, 'matchMedia').mockImplementation(query => ({ matches: state.mobile && query.includes('max-width'), media: query, onchange: null, addListener: () => {}, removeListener: () => {}, addEventListener: () => {}, removeEventListener: () => {}, dispatchEvent: () => false }))
  })

  it.each(['/overwatch', '/overwatch/condominiums/c1'])('restored mobile URL %s sends manager to attendance', path => {
    renderRoute(path)
    expect(screen.getByText('Attendance page')).toBeVisible()
    expect(screen.queryByText('Overwatch page')).not.toBeInTheDocument()
  })

  it('shows desktop-only notice without redirect loop when PlatformAdmin has no management access', () => {
    state.user.roles = ['PlatformAdmin']
    state.management = { isLoading: false, isSwitching: false, condominiumCount: 0, managementRoles: [], subManagerPermissions: [] }
    renderRoute('/overwatch/condominiums/c1')
    expect(screen.getByRole('heading', { name: /Overwatch/ })).toBeVisible()
    expect(screen.queryByText('Home')).not.toBeInTheDocument()
  })

  it('preserves direct Overwatch details on desktop', () => {
    state.mobile = false
    renderRoute('/overwatch/condominiums/c1')
    expect(screen.getByText('Overwatch page')).toBeVisible()
  })

  it('waits for management context on mobile instead of flashing the Overwatch notice', () => {
    state.user.roles = ['PlatformAdmin']
    state.management.isLoading = true
    renderRoute('/overwatch')
    expect(screen.queryByRole('heading', { name: /Overwatch/ })).not.toBeInTheDocument()
  })

  it('sends users without PlatformAdmin to the normal home', () => {
    state.user.roles = ['Manager']
    renderRoute('/overwatch')
    expect(screen.getByText('Home')).toBeVisible()
  })
})
