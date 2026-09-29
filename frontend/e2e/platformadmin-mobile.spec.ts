import { expect, test, type Page } from '@playwright/test'

const condominium = { id: 'condo-1', name: 'Aurora', isActive: true }
const counts = { open: 0, inProgress: 0, waitingForResident: 0, waitingForManager: 0, waitingForThirdParty: 0, waitingForResidentClosure: 0, resolved: 0, cancelled: 0 }

function tokenFor(roles: string[]) {
  const payload = Buffer.from(JSON.stringify({ exp: Math.floor(Date.now() / 1000) + 3600, role: roles })).toString('base64url')
  return `x.${payload}.x`
}

async function openAs(page: Page, roles: string[], theme: 'light' | 'dark' = 'light') {
  await page.addInitScript(({ token, theme }) => {
    localStorage.setItem('condolink.accessToken', token)
    localStorage.setItem('condolink.themePreference', theme)
  }, { token: tokenFor(roles), theme })
  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname
    if (path.endsWith('/users/me')) return route.fulfill({ json: { id: 'user-1', fullName: 'Platform User', email: 'platform@example.test', isActive: true, roles } })
    if (path.endsWith('/users/me/condominiums')) return route.fulfill({ json: roles.includes('Manager') ? [{ membershipId: 'membership-1', condominium, roles: ['Manager'], joinedAt: '2026-01-01T00:00:00Z', membershipActive: true }] : [] })
    if (path.endsWith('/management/context')) return route.fulfill({ json: { availableCondominiums: roles.includes('Manager') ? [condominium] : [], activeManagementCondominiumId: roles.includes('Manager') ? condominium.id : null, activeCondominium: roles.includes('Manager') ? condominium : null, condominiumCount: roles.includes('Manager') ? 1 : 0, managementRoles: roles.includes('Manager') ? ['Manager'] : [], subManagerPermissions: [], usesConsolidatedManagementScope: false } })
    if (path.endsWith('/notifications/unread-count')) return route.fulfill({ json: { unreadCount: 0 } })
    if (path.endsWith('/notifications')) return route.fulfill({ json: { items: [], unreadCount: 0 } })
    if (path.endsWith('/management/requests')) return route.fulfill({ json: { items: [], total: 0, page: 1, pageSize: 20, counts } })
    if (path.endsWith('/overwatch/management-companies')) return route.fulfill({ json: [] })
    return route.fulfill({ json: [] })
  })
}

test.describe('PlatformAdmin mobile context', () => {
  test('routes mobile PlatformAdmin + Manager to attendance; keeps Overwatch on desktop', async ({ page }, info) => {
    test.skip(info.project.name !== 'desktop-chromium', 'Viewport coverage runs in Chromium')
    test.setTimeout(120000)
    await openAs(page, ['PlatformAdmin', 'Manager'])
    for (const width of [320, 375, 390, 430]) {
      await page.setViewportSize({ width, height: 844 })
      await page.goto('/app')
      await expect(page).toHaveURL(/\/management\/requests$/)
      const nav = page.locator('.MuiBottomNavigation-root')
      await expect(nav.getByRole('button', { name: 'Atendimento' })).toBeVisible()
      await expect(nav.getByText('Overwatch')).toHaveCount(0)
      await nav.getByRole('button', { name: 'Mais' }).click()
      await expect(page.getByRole('heading', { name: 'Mais' })).toBeVisible()
      await expect(page.getByRole('button', { name: 'Overwatch' })).toHaveCount(0)

      // Models iOS/PWA restoring its last Overwatch URL on mobile.
      await page.goto('/overwatch/management-companies')
      await expect(page).toHaveURL(/\/management\/requests$/)
      await expect(page.locator('.MuiBottomNavigation-root').getByRole('button', { name: 'Atendimento' })).toBeVisible()
    }

    for (const width of [1024, 1440]) {
      await page.setViewportSize({ width, height: 1000 })
      await page.goto('/app')
      await expect(page).toHaveURL(/\/overwatch$/)
      await page.goto('/overwatch/management-companies')
      await expect(page).toHaveURL(/\/overwatch\/management-companies$/)
      await expect(page.getByRole('navigation').filter({ has: page.getByText('Administradoras') })).toBeVisible()
    }
  })

  test('shows desktop-only notice for mobile PlatformAdmin without management access', async ({ page }, info) => {
    test.skip(info.project.name !== 'desktop-chromium', 'Viewport coverage runs in Chromium')
    await openAs(page, ['PlatformAdmin'], 'dark')
    for (const width of [320, 375, 390, 430]) {
      await page.setViewportSize({ width, height: 844 })
      await page.goto('/app')
      await expect(page.getByRole('heading', { name: 'Overwatch disponível no desktop' })).toBeVisible()
      await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark')
      await expect(page.locator('.MuiBottomNavigation-root')).toHaveCount(0)
      await page.goto('/overwatch/condominiums/condo-1')
      await expect(page.getByRole('heading', { name: 'Overwatch disponível no desktop' })).toBeVisible()
      await expect(page.locator('.MuiBottomNavigation-root')).toHaveCount(0)
      await expect(page).toHaveURL(/\/overwatch\/condominiums\/condo-1$/)
    }
    for (const width of [1024, 1440]) {
      await page.setViewportSize({ width, height: 1000 })
      await page.goto('/overwatch')
      await expect(page).toHaveURL(/\/overwatch$/)
      await expect(page.locator('.MuiDrawer-root nav')).toBeVisible()
    }
  })
})
