import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ManagementCompanyIntegrations } from './ManagementCompanyIntegrations'

const api = vi.hoisted(() => ({ get: vi.fn() }))
vi.mock('../../services/api', () => ({ api }))

describe('ManagementCompanyIntegrations', () => {
  beforeEach(() => {
    api.get.mockReset().mockResolvedValue({ data: {
      provider: 'Superlogica', configured: false, status: 'NotConfigured', lastValidatedAt: null,
    } })
  })

  it('loads integration status once and keeps the loaded view stable', async () => {
    render(<ManagementCompanyIntegrations managementCompanyId="company-1" />)

    expect(await screen.findByRole('button', { name: 'Configurar integração' })).toBeVisible()
    await new Promise(resolve => window.setTimeout(resolve, 30))

    expect(api.get).toHaveBeenCalledTimes(1)
    expect(screen.getByRole('button', { name: 'Configurar integração' })).toBeVisible()
  })
})
