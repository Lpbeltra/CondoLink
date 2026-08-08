// spec: specs/comvy-ux-audit.md
// seed: seed.spec.ts

import { expect, test } from '@playwright/test'

test.describe('Login válido, inválido e troca obrigatória de senha', () => {
  test('Login inválido informa o erro sem sair da tela', async ({ page }) => {
    const pageErrors: string[] = []
    page.on('pageerror', error => pageErrors.push(error.message))

    // 1. Acessar o formulário de login.
    await page.goto('/login')

    // 2. Informar credenciais inválidas e enviar o formulário.
    await page.getByRole('textbox', { name: 'E-mail' }).fill('invalido@example.com')
    await page.getByRole('textbox', { name: 'Senha' }).fill('senha-incorreta')
    const loginResponsePromise = page.waitForResponse(response =>
      response.url().includes('/api/auth/login') && response.request().method() === 'POST')
    await page.getByRole('button', { name: 'Entrar' }).click()

    // 3. Verificar a resposta não autorizada e a mensagem acessível.
    const loginResponse = await loginResponsePromise
    expect(loginResponse.status()).toBe(401)
    await expect(page.getByRole('alert')).toContainText('E-mail ou senha incorretos')
    await expect(page).toHaveURL(/\/login$/)
    expect(pageErrors, 'erros JavaScript não tratados').toEqual([])
  })
})
