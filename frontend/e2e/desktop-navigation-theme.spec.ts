// spec: specs/comvy-ux-audit.md
// seed: seed.spec.ts

import { expect, test } from '@playwright/test'
import { loginAsManager, observeUnexpectedRuntimeErrors } from './support'

test.describe('Navegação desktop/mobile e temas', () => {
  test('Sidebar desktop navega e alterna os temas claro e escuro', async ({ page }, testInfo) => {
    test.skip(testInfo.project.name === 'mobile-chromium', 'Cenário específico da sidebar desktop')
    const assertRuntimeIsClean = observeUnexpectedRuntimeErrors(page)

    // 1. Entrar como gestor e verificar a navegação lateral.
    await loginAsManager(page)
    const navigation = page.getByRole('navigation', { name: 'Navegação principal' })
    await expect(navigation).toBeVisible()
    await expect(navigation.getByRole('link', { name: 'Dashboard' })).toBeVisible()
    await expect(navigation.getByRole('link', { name: 'Atendimento' })).toBeVisible()

    // 2. Navegar pela sidebar até Atendimento.
    await navigation.getByRole('link', { name: 'Atendimento' }).click()
    await expect(page).toHaveURL(/\/management\/requests/)
    await expect(page.getByRole('heading', { name: 'Atendimento' })).toBeVisible()

    // 3. Alternar explicitamente para os temas claro e escuro.
    await page.getByRole('button', { name: 'Alternar para o tema claro' }).click()
    await expect(page.locator('html')).toHaveAttribute('data-theme', 'light')
    await page.getByRole('button', { name: 'Alternar para o tema escuro' }).click()
    await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark')
    assertRuntimeIsClean()
  })
})
