import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ManagementCompanyIntegrations } from './ManagementCompanyIntegrations'

const api = vi.hoisted(() => ({ get: vi.fn(), put: vi.fn() }))
vi.mock('../../services/api', () => ({ api }))

describe('ManagementCompanyIntegrations', () => {
  beforeEach(() => {
    api.put.mockReset()
    api.get.mockReset().mockResolvedValue({ data: {
      provider: 'Superlogica', configured: false, status: 'NotConfigured', lastValidatedAt: null,
    } })
  })

  it('loads integration status once and keeps the loaded view stable', async () => {
    render(<MemoryRouter><ManagementCompanyIntegrations managementCompanyId="company-1" /></MemoryRouter>)

    expect(await screen.findByRole('button', { name: 'Configurar integração' })).toBeVisible()
    await new Promise(resolve => window.setTimeout(resolve, 30))

    expect(api.get).toHaveBeenCalledTimes(1)
    expect(screen.getByRole('button', { name: 'Configurar integração' })).toBeVisible()
  })

  it('labels Superlogica Token and sends credentials to matching API fields', async () => {
    const user = userEvent.setup()
    api.put.mockResolvedValue({ data: { success: true, message: 'Conexão validada.', integration: {
      provider: 'Superlogica', configured: true, status: 'Connected', lastValidatedAt: null,
    } } })
    render(<MemoryRouter><ManagementCompanyIntegrations managementCompanyId="company-1" /></MemoryRouter>)
    await user.click(await screen.findByRole('button', { name: 'Configurar integração' }))

    await user.type(screen.getByLabelText('Token'), 'TOKEN_TESTE_123')
    await user.type(screen.getByLabelText('Access Token'), 'ACCESS_TESTE_456')
    await user.type(screen.getByLabelText('Secret'), 'SECRET_TESTE_789')
    await user.click(screen.getByRole('button', { name: 'Salvar e testar conexão' }))

    await waitFor(() => expect(api.put).toHaveBeenCalledWith(
      '/overwatch/management-companies/company-1/integrations/superlogica',
      { appToken: 'TOKEN_TESTE_123', accessToken: 'ACCESS_TESTE_456', secret: 'SECRET_TESTE_789' },
    ))
    expect(screen.queryByLabelText('App Token')).not.toBeInTheDocument()
  })
})
