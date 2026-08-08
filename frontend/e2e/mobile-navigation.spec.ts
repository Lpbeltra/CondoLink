// spec: specs/comvy-ux-audit.md
// seed: seed.spec.ts

import { expect, test } from '@playwright/test'
import { loginAsManager, observeUnexpectedRuntimeErrors } from './support'

test.describe('Navegação desktop/mobile e temas', () => {
  test('Navegação inferior mobile abre os atalhos de gestão', async ({ page }, testInfo) => {
    test.skip(testInfo.project.name !== 'mobile-chromium', 'Cenário específico da navegação mobile')
    const assertRuntimeIsClean = observeUnexpectedRuntimeErrors(page)

    // 1. Entrar como gestor no viewport mobile e verificar a barra inferior.
    await loginAsManager(page)
    const navigation = page.getByLabel('Navegação principal').filter({
      has: page.getByRole('button', { name: 'Mais' }),
    })
    await expect(navigation).toBeVisible()
    await expect(navigation.getByRole('button', { name: 'Dashboard' })).toBeVisible()
    await expect(navigation.getByRole('button', { name: 'Mais' })).toBeVisible()

    // 2. Abrir Mais e acessar Atendimento.
    await navigation.getByRole('button', { name: 'Mais' }).click()
    await expect(page.getByRole('heading', { name: 'Mais' })).toBeVisible()
    await page.getByRole('button', { name: 'Atendimento' }).click()
    await expect(page).toHaveURL(/\/management\/requests/)
    await expect(page.getByRole('heading', { name: 'Atendimento' })).toBeVisible()
    assertRuntimeIsClean()
  })
})
