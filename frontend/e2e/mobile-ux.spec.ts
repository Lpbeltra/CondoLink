import { expect, test, type Page, type TestInfo } from '@playwright/test'

test.use({ serviceWorkers: 'block' })
test.describe.configure({ timeout: 120_000 })
const ui = expect.configure({ timeout: 20_000 })

// All API traffic is intercepted. These scenarios never use a real account or database.
const condo = { id: 'c1', name: 'Condomínio Monticello', isActive: true }
const now = '2026-09-28T12:00:00Z'
const request = {
  id: 'r1', protocol: 'ABC12345', condominiumId: condo.id, condominiumName: condo.name,
  title: 'Portão da garagem precisa de manutenção', status: 'Open', priority: 'Normal',
  author: { id: 'resident1', fullName: 'Maria de Souza' }, category: { id: 'cat1', name: 'Manutenção' },
  targetUnit: { id: 'u1', identifier: '101', block: 'A' }, createdAt: now, updatedAt: now,
  resolvedAt: null, description: 'Portão não fecha completamente.', statusHistory: [], aiAnalysis: null,
  originalReport: null, residentReplyRequirement: null, canManageInternalNotes: true,
}
const reminder = {
  id: 'a1', title: 'Inspeção dos extintores e equipamentos de segurança', description: 'Conferir todos os blocos com o prestador responsável.',
  unitId: 'u1', unitIdentifier: '101', block: 'A', relatedThirdParty: 'Prestador com nome longo para validar quebra de texto',
  startsAtUtc: now, nextOccurrenceAtUtc: now, timeZoneId: 'America/Sao_Paulo', recurrenceType: 'Weekly',
  notifyByWhatsApp: false, notifyByEmail: false, isActive: true, completedAt: null, createdAt: now,
  requestCount: 1, requestIds: ['r1'], linkedRequests: [{ id: 'r1', protocol: 'ABC12345', title: request.title }],
}
const member = {
  membershipId: 'm1', userId: 'resident1', fullName: 'Maria de Souza com sobrenome extenso',
  email: 'maria.sobrenome.extenso@example.test', phoneNumber: '11999998888', userActive: true,
  mustChangePassword: false, emailDeliveryEnabled: true, firstAccessStatus: 'Completed',
  lastLoginAt: now, membershipActive: true, joinedAt: now, endedAt: null, roles: ['Resident'],
  unitLinks: [{ unitMembershipId: 'um1', unitId: 'u1', unitIdentifier: '101', block: 'A', relationshipType: 'Owner', isResident: true, isPrimaryResidence: true }],
}

async function mockApp(page: Page, mode: 'light' | 'dark') {
  const token = `e30.${Buffer.from(JSON.stringify({ exp: 4102444800, role: 'Manager' })).toString('base64url')}.mock`
  await page.addInitScript(({ token, mode }) => {
    localStorage.setItem('condolink.accessToken', token)
    localStorage.setItem('condolink.themePreference', mode)
  }, { token, mode })
  const runtimeErrors: string[] = []
  page.on('pageerror', error => runtimeErrors.push(error.message))
  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname.replace(/^\/api/, '')
    let body: unknown = []
    if (path === '/users/me') body = { id: 'manager1', fullName: 'Gestor local', email: 'manager@example.test', isActive: true, roles: ['Manager'] }
    else if (path.includes('/notifications')) body = { items: [], total: 0, unreadCount: 0, page: 1, pageSize: 20 }
    else if (path === '/management/reports/requests') body = { period: { from: now, to: now, days: 30 }, summary: { total: 0, open: 0, awaitingFirstResponse: 0, averageFirstResponseHours: null, averageResolutionHours: null, resolutionRatePercent: null }, byCategory: [], byPriority: [], createdPerDay: [] }
    else if (path === '/users/me/condominiums') body = [{ membershipId: 'manager-membership', condominium: condo, roles: ['Manager'], membershipActive: true, joinedAt: now }]
    else if (path === '/management/context') body = { availableCondominiums: [condo], activeManagementCondominiumId: 'c1', activeCondominium: condo, condominiumCount: 1, usesConsolidatedManagementScope: false, managementRoles: ['Manager'] }
    else if (path === '/management/requests') body = { items: [request, { ...request, id: 'r2', title: 'Iluminação da área comum' }], total: 2, page: 1, pageSize: 20, counts: { open: 2, inProgress: 0, waitingForResident: 0, waitingForManager: 0, waitingForThirdParty: 0, waitingForResidentClosure: 0, resolved: 0, cancelled: 0 } }
    else if (path === '/requests/r1') body = request
    else if (path === '/condominiums/c1/members') body = [member]
    else if (path === '/condominiums/c1/units') body = [{ id: 'u1', condominiumId: 'c1', identifier: '101', blockId: 'b1', block: 'A', floor: '1', description: null, isActive: true, createdAt: now, updatedAt: now }]
    else if (path === '/management/condominiums/c1/agenda') body = [reminder]
    else if (path.endsWith('/assistant/conversations')) body = { items: [], hasMore: false, total: 0 }
    await route.fulfill({ json: body })
  })
  return { runtimeErrors }
}

async function noOverflow(page: Page) {
  const size = await page.evaluate(() => ({ viewport: document.documentElement.clientWidth, content: document.documentElement.scrollWidth }))
  expect(size.content, 'document horizontal overflow').toBeLessThanOrEqual(size.viewport)
}

async function inputsKeepScale(page: Page) {
  const controls = page.locator('input:not([type="hidden"]):not([type="checkbox"]):visible, textarea:visible, [role="combobox"]:visible')
  for (const control of await controls.all()) {
    expect(await control.evaluate(element => parseFloat(getComputedStyle(element).fontSize))).toBeGreaterThanOrEqual(16)
  }
  const input = page.locator('input:not([type="hidden"]):not([type="checkbox"]):visible, textarea:visible').first()
  if (await input.count()) {
    const before = await page.evaluate(() => window.visualViewport?.scale ?? 1)
    await input.focus()
    await input.blur()
    expect(await page.evaluate(() => window.visualViewport?.scale ?? 1)).toBe(before)
  }
  expect(await page.locator('meta[name="viewport"]').getAttribute('content')).not.toMatch(/user-scalable\s*=\s*no|maximum-scale\s*=\s*1(?:\D|$)/)
}

async function capture(page: Page, info: TestInfo, label: string) {
  await noOverflow(page)
  await info.attach(label, { body: await page.screenshot({ fullPage: true }), contentType: 'image/png' })
}

for (const width of [320, 375, 390, 430]) {
  for (const mode of ['light', 'dark'] as const) {
    test(`mobile UX ${width}px ${mode}`, async ({ page }, info) => {
      await page.setViewportSize({ width, height: 844 })
      const mock = await mockApp(page, mode)
      await page.goto('/')
      await ui(page).toHaveURL(/\/management\/requests/)
      await ui(page.locator('html')).toHaveAttribute('data-theme', mode)
      const summaries = page.getByRole('group', { name: 'Resumo dos atendimentos' })
      await ui(summaries).toBeVisible()
      expect((await summaries.boundingBox())!.height).toBeLessThanOrEqual(70)
      expect(await summaries.evaluate(element => element.scrollWidth > element.clientWidth)).toBe(true)
      const cards = page.locator('article')
      await ui(cards).toHaveCount(2)
      expect(await cards.first().evaluate(element => parseFloat(getComputedStyle(element).borderTopWidth))).toBeGreaterThan(0)
      expect((await cards.nth(1).boundingBox())!.y).toBeGreaterThan((await cards.first().boundingBox())!.y + (await cards.first().boundingBox())!.height)
      await inputsKeepScale(page)
      await capture(page, info, 'requests')

      await cards.first().click()
      const actions = page.getByRole('heading', { name: 'Ações de atendimento' }).locator('..')
      await ui(actions).toBeVisible()
      const opened = await page.getByText(/^Aberto em/).first().boundingBox()
      const actionBox = await actions.boundingBox()
      const tabs = await page.getByRole('tablist', { name: 'Áreas do atendimento' }).boundingBox()
      expect(actionBox!.y).toBeGreaterThanOrEqual(opened!.y + opened!.height)
      expect(actionBox!.y + actionBox!.height).toBeLessThanOrEqual(tabs!.y)
      for (const button of await actions.getByRole('button').all()) {
        const box = await button.boundingBox()
        expect(box!.x + box!.width).toBeLessThanOrEqual(width)
      }
      await capture(page, info, 'request-detail')

      await page.goto('/management/people')
      await ui(page.getByRole('heading', { name: 'Moradores', exact: true })).toBeVisible()
      await ui(page.getByRole('button', { name: 'Exportar moradores em PDF', exact: true })).toHaveCount(0)
      await page.getByRole('button', { name: 'Mais ações de moradores' }).click()
      await ui(page.getByRole('menuitem', { name: 'Exportar moradores em PDF' })).toBeVisible()
      await page.keyboard.press('Escape')
      await capture(page, info, 'people')
      await page.getByRole('button', { name: 'Morador', exact: true }).click()
      const dialog = page.getByRole('dialog')
      await dialog.getByRole('textbox', { name: 'Nome completo' }).fill('Novo Morador Mobile')
      await dialog.getByRole('textbox', { name: 'E-mail', exact: true }).fill('novo@example.test')
      await dialog.getByRole('textbox', { name: 'Telefone / WhatsApp' }).fill('(11) 98888-7777')
      await dialog.getByRole('combobox', { name: /Associar a uma unidade/ }).fill('101')
      await page.getByRole('option', { name: /Apto 101/ }).click()
      await dialog.getByRole('checkbox', { name: 'Reside na unidade' }).check()
      await inputsKeepScale(page)
      await capture(page, info, 'resident-form')
      await dialog.getByRole('button', { name: 'Cancelar', exact: true }).click()

      await page.goto('/management/agenda')
      const row = page.getByRole('listitem').filter({ hasText: reminder.title })
      await ui(row).toBeVisible()
      const title = await row.getByText(reminder.title, { exact: true }).boundingBox()
      const complete = await row.getByRole('button', { name: `Concluir ${reminder.title}`, exact: true }).boundingBox()
      const description = await row.getByText(reminder.description, { exact: true }).boundingBox()
      expect(title!.y + title!.height).toBeLessThanOrEqual(complete!.y)
      expect(complete!.y + complete!.height).toBeLessThanOrEqual(description!.y)
      await inputsKeepScale(page)
      await capture(page, info, 'agenda')

      await page.goto('/management/assistant')
      const suggestions = page.locator('#assistant-suggestions')
      await ui(suggestions.getByRole('button')).toHaveCount(2)
      await page.getByRole('button', { name: 'Mais sugestões' }).click()
      await ui(suggestions.getByRole('button')).toHaveCount(6)
      await suggestions.getByRole('button', { name: /Encontre um morador/ }).click()
      await ui(page.getByRole('textbox', { name: 'Pergunte ao assistente' })).toHaveValue('Encontre um morador')
      await page.getByRole('button', { name: 'Menos sugestões' }).click()
      await inputsKeepScale(page)
      await capture(page, info, 'assistant')
      expect(mock.runtimeErrors).toEqual([])
    })
  }
}

test('desktop keeps dashboard entry and six assistant suggestions', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 1000 })
  await mockApp(page, 'light')
  await page.goto('/')
  await ui(page).toHaveURL(/\/management\/dashboard/)
  await page.goto('/management/assistant')
  await ui(page.locator('#assistant-suggestions').getByRole('button')).toHaveCount(6)
  await ui(page.getByRole('button', { name: 'Mais sugestões' })).toHaveCount(0)
  await noOverflow(page)

  for (const width of [1024, 1280, 1440]) {
    await page.setViewportSize({ width, height: 1000 })
    await page.goto('/management/requests')
    const cards = page.locator('article')
    await ui(cards.first()).toBeVisible()
    await cards.first().click()
    const actions = page.getByRole('heading', { name: 'Ações de atendimento' }).locator('..')
    await ui(actions).toBeVisible()
    for (const button of await actions.getByRole('button').all()) {
      expect(await button.evaluate(element => getComputedStyle(element).whiteSpace)).toBe('nowrap')
    }
    await noOverflow(page)
  }
})
