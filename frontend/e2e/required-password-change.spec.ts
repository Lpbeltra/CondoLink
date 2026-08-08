// spec: specs/comvy-ux-audit.md
// seed: seed.spec.ts

import { expect, test } from '@playwright/test'

test.describe('Login válido, inválido e troca obrigatória de senha', () => {
  test('Senha temporária encaminha para a troca obrigatória', async ({ page }) => {
    // 1. Simular de forma determinística uma conta com senha temporária.
    await page.route('**/api/auth/login', async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          requiresPasswordChange: true,
          email: 'temporario@example.com',
        }),
      })
    })

    // 2. Acessar o login e autenticar com a senha temporária.
    await page.goto('/login')
    await page.getByRole('textbox', { name: 'E-mail' }).fill('temporario@example.com')
    await page.getByRole('textbox', { name: 'Senha' }).fill('Temporaria2026')
    await page.getByRole('button', { name: 'Entrar' }).click()

    // 3. Verificar a tela obrigatória e os dados transportados do login.
    await expect(page).toHaveURL(/\/change-password$/)
    await expect(page.getByRole('heading', { name: 'Alterar senha' })).toBeVisible()
    await expect(page.getByRole('textbox', { name: 'E-mail' })).toHaveValue('temporario@example.com')
    await expect(page.getByLabel('Senha temporária')).toHaveValue('Temporaria2026')
    await expect(page.getByRole('button', { name: 'Atualizar senha' })).toBeDisabled()
  })
})
