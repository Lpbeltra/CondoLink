import { expect, test } from '@playwright/test'

async function openAuthenticatedShell(page: import('@playwright/test').Page) {
  const payload = btoa(JSON.stringify({ exp: Math.floor(Date.now() / 1000) + 3600, role: 'PlatformAdmin' }))
  await page.addInitScript(token => localStorage.setItem('condolink.accessToken', token), `x.${payload}.x`)
  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname
    if (path.endsWith('/users/me')) return route.fulfill({ json: {
      id: '11111111-1111-1111-1111-111111111111', fullName: 'Maria',
      email: 'maria@example.com', isActive: true, roles: ['PlatformAdmin'],
    } })
    if (path.endsWith('/users/me/condominiums')) return route.fulfill({ json: [] })
    if (path.endsWith('/management/context')) return route.fulfill({ json: {
      activeManagementCondominiumId: null, usesConsolidatedManagementScope: false,
      availableCondominiums: [],
    } })
    if (path.endsWith('/notifications/unread-count')) return route.fulfill({ json: { unreadCount: 0 } })
    if (path.endsWith('/notifications')) return route.fulfill({ json: { items: [], unreadCount: 0 } })
    return route.fulfill({ json: [] })
  })
  await page.goto('/')
  await expect(page.getByRole('heading', { name: 'Olá, Maria' })).toBeVisible()
}

async function dispatchInstallPrompt(page: import('@playwright/test').Page) {
  await page.evaluate(() => {
    const event = new Event('beforeinstallprompt', { cancelable: true })
    Object.defineProperties(event, {
      prompt: { value: () => Promise.resolve() },
      userChoice: { value: Promise.resolve({ outcome: 'dismissed', platform: 'web' }) },
    })
    window.dispatchEvent(event)
  })
}

test.describe('Descoberta da instalação do PWA', () => {
  test('mobile mostra, dispensa e mantém o banner oculto durante a navegação', async ({ page }, testInfo) => {
    test.skip(testInfo.project.name !== 'mobile-chromium', 'Cenário mobile Chromium')
    await openAuthenticatedShell(page)
    await dispatchInstallPrompt(page)

    const banner = page.getByRole('region', { name: 'Instalar Comvy' })
    await expect(banner).toBeVisible()
    await banner.getByRole('button', { name: 'Agora não' }).click()
    await expect(banner).toBeHidden()
    await page.reload()
    await dispatchInstallPrompt(page)
    await expect(banner).toBeHidden()
    await expect(page.getByRole('heading', { name: 'Olá, Maria' })).toBeVisible()
  })

  test('desktop não mostra o banner mesmo com evento de instalação', async ({ page }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop-chromium', 'Cenário desktop Chromium')
    await openAuthenticatedShell(page)
    await dispatchInstallPrompt(page)
    await expect(page.getByRole('region', { name: 'Instalar Comvy' })).toHaveCount(0)
  })
})
