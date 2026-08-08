// spec: specs/comvy-ux-audit.md
// seed: seed.spec.ts

import { expect, test } from '@playwright/test'
import { loginAsManager, observeUnexpectedRuntimeErrors } from './support'

const statusLabels = [
  'Aberta',
  'Em andamento',
  'Aguardando morador',
  'Dar andamento',
  'Aguardando terceiro',
  'Resolvida',
  'Cancelada',
]

const priorityLabels = ['Normal', 'Alta', 'Urgente']

test.describe('Dashboard, listagem, filtros e estados dos atendimentos', () => {
  test('Listagem filtra solicitações e abre um detalhe sem erros inesperados', async ({ page }, testInfo) => {
    const assertRuntimeIsClean = observeUnexpectedRuntimeErrors(page)

    // 1. Entrar como gestor e abrir a listagem de Atendimento.
    await loginAsManager(page)
    await page.goto('/management/requests')
    await expect(page.getByRole('heading', { name: 'Atendimento' })).toBeVisible()

    // 2. Verificar os resumos e filtros com nomes acessíveis.
    for (const summary of ['Abertas', 'Em andamento', 'Aguardando morador', 'Dar andamento', 'Aguardando terceiro', 'Resolvidas', 'Canceladas']) {
      await expect(page.getByRole('button', { name: new RegExp(`Filtrar por ${summary}`) })).toBeVisible()
    }
    const statusFilter = page.getByRole('combobox', { name: 'Status' })
    const priorityFilter = page.getByRole('combobox', { name: 'Prioridade' })
    await expect(statusFilter).toBeVisible()
    await expect(priorityFilter).toBeVisible()

    // 3. Filtrar por solicitações abertas e validar o estado refletido na URL.
    await page.getByRole('button', { name: /Filtrar por Abertas/ }).click()
    await expect(page).toHaveURL(/(?:\?|&)status=Open(?:&|$)/)
    await expect(statusFilter).toContainText('Aberta')

    // 4. Validar que o card expõe status e prioridade conhecidos.
    const requestCard = page.getByRole('button').filter({
      has: page.getByRole('heading', { level: 3 }),
    }).first()
    await expect(requestCard).toBeVisible()
    const visibleStatuses = await Promise.all(statusLabels.map(label => requestCard.getByText(label, { exact: true }).isVisible()))
    const visiblePriorities = await Promise.all(priorityLabels.map(label => requestCard.getByText(label, { exact: true }).isVisible()))
    expect(visibleStatuses.filter(Boolean)).toHaveLength(1)
    expect(visiblePriorities.filter(Boolean)).toHaveLength(1)
    await testInfo.attach('atendimento-listagem', {
      body: await page.screenshot({ fullPage: true }),
      contentType: 'image/png',
    })

    // 5. Abrir o primeiro atendimento filtrado sem executar ações mutáveis.
    await requestCard.click()
    await expect(page).toHaveURL(/\/management\/requests\/[^/?]+/)
    await expect(page.getByRole('button', { name: 'Voltar' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Descrição' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Ações de atendimento' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Histórico de status' })).toBeVisible()
    await expect(page.getByRole('button', { name: 'Alterar status' })).toBeVisible()
    await expect(page.getByRole('button', { name: 'Alterar prioridade' })).toBeVisible()

    // 6. Registrar o detalhe e confirmar console/rede limpos.
    await testInfo.attach('atendimento-detalhe', {
      body: await page.screenshot({ fullPage: true }),
      contentType: 'image/png',
    })
    assertRuntimeIsClean()
  })
})
