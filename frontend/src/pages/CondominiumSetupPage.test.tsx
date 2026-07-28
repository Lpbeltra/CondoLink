import { fireEvent, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { MemoryRouter } from 'react-router-dom'

const setupApi = vi.hoisted(() => ({
  previewSetup: vi.fn(),
  previewSetupImport: vi.fn(),
  previewGeneratedSetup: vi.fn(),
  confirmSetup: vi.fn(),
  downloadSetupTemplate: vi.fn(),
}))

vi.mock('../management/ManagementContext', () => ({
  useManagementContext: () => ({
    activeCondominiumId: 'condo-1',
  }),
}))
vi.mock('../management/setupApi', () => setupApi)

import { CondominiumSetupPage } from './CondominiumSetupPage'

const renderPage = () => render(
  <MemoryRouter><CondominiumSetupPage /></MemoryRouter>,
)

const preview = {
  draft: {
    noRegistrableUnits: false,
    units: [{
      line: 2,
      block: 'Tower A',
      unit: '01',
      floor: 'Ground',
      description: null,
    }],
    residents: [],
  },
  blocks: [{ identifier: 'Tower A', existing: false }],
  units: [{
    line: 2,
    block: 'Tower A',
    unit: '01',
    floor: 'Ground',
    description: null,
    existing: false,
  }],
  residents: [],
  warnings: [],
  errors: [],
  totals: {
    blocks: 1,
    units: 1,
    residents: 0,
    existingUsers: 0,
    newUsers: 0,
  },
}

describe('CondominiumSetupPage', () => {
  beforeEach(() => {
    Object.values(setupApi).forEach(mock => mock.mockReset())
    setupApi.previewSetupImport.mockResolvedValue(preview)
    setupApi.previewSetup.mockResolvedValue({
      ...preview,
      draft: { ...preview.draft, units: [] },
      units: [],
      totals: { ...preview.totals, units: 0 },
    })
    setupApi.confirmSetup.mockResolvedValue({
      blocksCreated: 1,
      unitsCreated: 1,
      residentsLinked: 0,
      credentials: [],
      message: 'Configuração concluída com sucesso.',
    })
  })

  it('teaches spreadsheet import and downloads both templates', async () => {
    const user = userEvent.setup()
    renderPage()

    expect(screen.getByText('Escolher método')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Continuar' }))

    expect(screen.getByText(/Nada será salvo agora/)).toBeInTheDocument()
    expect(screen.getByText(/Não renomeie as colunas/)).toBeInTheDocument()
    expect(screen.getByRole('columnheader', {
      name: 'Obrigatória',
    })).toBeInTheDocument()
    expect(screen.getByText('101 / 01 / House 4')).toBeInTheDocument()
    await user.click(screen.getByRole('button', {
      name: 'Baixar modelo de estrutura',
    }))
    await user.click(screen.getByRole('button', {
      name: 'Baixar modelo de moradores',
    }))
    expect(setupApi.downloadSetupTemplate).toHaveBeenNthCalledWith(
      1, 'condo-1', 'structure',
    )
    expect(setupApi.downloadSetupTemplate).toHaveBeenNthCalledWith(
      2, 'condo-1', 'residents',
    )
  })

  it('imports, previews, removes a row and confirms without partial actions', async () => {
    const user = userEvent.setup()
    const { container } = renderPage()
    await user.click(screen.getByRole('button', { name: 'Continuar' }))
    const fileInputs = container.querySelectorAll<HTMLInputElement>(
      'input[type="file"]',
    )
    fireEvent.change(fileInputs[0], {
      target: {
        files: [new File(['Block,Unit\nTower A,01'], 'structure.csv')],
      },
    })
    await user.click(screen.getByRole('button', {
      name: 'Gerar e validar prévia',
    }))

    expect(await screen.findByText('Lote validado. Revise os dados antes de confirmar.')).toBeInTheDocument()
    expect(screen.getByText('1 unidades')).toBeInTheDocument()
    await user.click(screen.getByRole('button', {
      name: 'Remover unidade 01',
    }))
    expect(setupApi.previewSetup).toHaveBeenCalledWith(
      'condo-1',
      expect.objectContaining({ units: [] }),
    )
    expect(await screen.findByText('0 unidades')).toBeInTheDocument()
    await user.click(screen.getByRole('button', {
      name: 'Confirmar configuração',
    }))
    expect(await screen.findByText(
      'Configuração concluída com sucesso.',
    )).toBeInTheDocument()
    expect(setupApi.confirmSetup).toHaveBeenCalled()
  })

  it('shows generator help on every field and supports narrow screens', async () => {
    const originalMatchMedia = window.matchMedia
    window.matchMedia = vi.fn().mockImplementation(query => ({
      matches: query.includes('max-width'),
      media: query,
      onchange: null,
      addListener: vi.fn(),
      removeListener: vi.fn(),
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      dispatchEvent: vi.fn(),
    }))
    const user = userEvent.setup()
    const { container } = renderPage()
    await user.click(screen.getByLabelText(
      'Gerar estrutura por torres e segmentos',
    ))
    await user.click(screen.getByRole('button', { name: 'Continuar' }))

    expect(screen.getByText(
      'Número de unidades geradas em cada andar.',
    )).toBeInTheDocument()
    expect(screen.getByText(
      'Valor opcional antes do número, como A-.',
    )).toBeInTheDocument()
    expect(screen.getByText('Numerações suportadas')).toBeInTheDocument()
    expect(container.querySelector('.MuiStepper-vertical')).not.toBeNull()
    window.matchMedia = originalMatchMedia
  })

  it('explains errors with line, column and reason', async () => {
    setupApi.previewSetupImport.mockResolvedValue({
      ...preview,
      errors: [{
        line: 3,
        column: 'Unit',
        reason: 'Unidade duplicada no lote.',
      }],
    })
    const user = userEvent.setup()
    const { container } = renderPage()
    await user.click(screen.getByRole('button', { name: 'Continuar' }))
    const input = container.querySelector<HTMLInputElement>(
      'input[type="file"]',
    )!
    fireEvent.change(input, {
      target: { files: [new File(['x'], 'structure.csv')] },
    })
    await user.click(screen.getByRole('button', {
      name: 'Gerar e validar prévia',
    }))

    const error = await screen.findByText(/Linha 3, Unit/)
    expect(within(error.parentElement!).getByText(
      /Unidade duplicada no lote/,
    )).toBeInTheDocument()
    expect(screen.getByRole('button', {
      name: 'Confirmar configuração',
    })).toBeDisabled()
  })
})
