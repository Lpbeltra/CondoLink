import { expect, type Page } from '@playwright/test'

export const managerCredentials = {
  email: process.env.COMVY_E2E_MANAGER_EMAIL ?? '',
  password: process.env.COMVY_E2E_MANAGER_PASSWORD ?? '',
}

export async function loginAsManager(page: Page) {
  if (!managerCredentials.email || !managerCredentials.password) {
    throw new Error('Defina COMVY_E2E_MANAGER_EMAIL e COMVY_E2E_MANAGER_PASSWORD para executar os cenários autenticados.')
  }
  await page.goto('/login')
  await page.getByRole('textbox', { name: 'E-mail' }).fill(managerCredentials.email)
  await page.getByRole('textbox', { name: 'Senha' }).fill(managerCredentials.password)
  await page.getByRole('button', { name: 'Entrar' }).click()
  await expect(page).toHaveURL(/\/management\/dashboard|\/$/)
}

export function observeUnexpectedRuntimeErrors(page: Page) {
  const consoleErrors: string[] = []
  const pageErrors: string[] = []
  const networkErrors: string[] = []

  page.on('console', message => {
    if (message.type() === 'error') consoleErrors.push(message.text())
  })
  page.on('pageerror', error => pageErrors.push(error.message))
  page.on('requestfailed', request => {
    if (!request.url().includes('/api/')) return
    const failure = request.failure()?.errorText ?? 'falha desconhecida'
    if (!failure.includes('ERR_ABORTED')) networkErrors.push(`${request.method()} ${request.url()}: ${failure}`)
  })
  page.on('response', response => {
    if (response.url().includes('/api/') && response.status() >= 400) {
      networkErrors.push(`${response.request().method()} ${response.url()}: HTTP ${response.status()}`)
    }
  })

  return () => {
    expect(consoleErrors, 'erros inesperados no console').toEqual([])
    expect(pageErrors, 'erros JavaScript não tratados').toEqual([])
    expect(networkErrors, 'erros inesperados de API/rede').toEqual([])
  }
}
