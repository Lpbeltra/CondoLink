// spec: specs/comvy-ux-audit.md
// seed: seed.spec.ts

import { expect, test } from '@playwright/test'
import { loginAsManager, observeUnexpectedRuntimeErrors } from './support'

test.describe('Login válido, inválido e troca obrigatória de senha', () => {
  test('Login válido leva o gestor ao dashboard', async ({ page }, testInfo) => {
    const assertRuntimeIsClean = observeUnexpectedRuntimeErrors(page)

    // 1. Acessar o login e autenticar com a conta local de gestão.
    await loginAsManager(page)

    // 2. Verificar o destino autenticado e a identidade do gestor.
    await expect(page.getByRole('heading', { name: /Olá, Lisandro/ })).toBeVisible()
    await expect(page.getByText('Você administra 2 condomínios.')).toBeVisible()

    // 3. Registrar uma evidência visual do dashboard autenticado.
    await testInfo.attach('dashboard-autenticado', {
      body: await page.screenshot({ fullPage: true }),
      contentType: 'image/png',
    })
    assertRuntimeIsClean()
  })
})
