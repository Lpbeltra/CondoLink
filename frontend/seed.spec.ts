import { test, expect } from '@playwright/test'

test.describe('Comvy management seed', () => {
  test('seed', async ({ page }) => {
    const email = process.env.COMVY_E2E_MANAGER_EMAIL
    const password = process.env.COMVY_E2E_MANAGER_PASSWORD
    test.skip(!email || !password, 'Credenciais E2E de gestão não configuradas')
    await page.goto('/login')
    await page.getByRole('textbox', { name: 'E-mail' }).fill(email!)
    await page.getByRole('textbox', { name: 'Senha' }).fill(password!)
    await page.getByRole('button', { name: 'Entrar' }).click()
    await expect(page).toHaveURL(/\/management\/dashboard|\/$/)
  })
})
