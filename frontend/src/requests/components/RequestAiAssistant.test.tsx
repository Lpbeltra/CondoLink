import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { OriginalReportAccordion } from './OriginalReportAccordion'
import { canViewInternalRequestDetails, confidenceLabel, RequestAiAssistant } from './RequestAiAssistant'
import type { RequestAiAnalysis } from '../types'

const analysis = (overrides: Partial<RequestAiAnalysis> = {}): RequestAiAnalysis => ({
  title: 'Título gerado',
  description: 'Descrição gerada',
  suggestedCategory: 'Jardinagem',
  confidence: 0.82,
  missingInformation: ['Informar quando o problema começou'],
  generatedAt: '2026-07-31T12:00:00Z',
  model: 'gpt-test',
  ...overrides,
})

describe('RequestAiAssistant', () => {
  it('shows administrative insights without repeating title, description or model', () => {
    render(<RequestAiAssistant analysis={analysis()} />)

    expect(screen.getByText('Assistente Comvy')).toBeInTheDocument()
    expect(screen.getByText('Jardinagem')).toBeInTheDocument()
    expect(screen.getByText('Alta')).toBeInTheDocument()
    expect(screen.getByText('Possíveis informações pendentes')).toBeInTheDocument()
    expect(screen.getByText('Informar quando o problema começou')).toBeInTheDocument()
    expect(screen.queryByText('Título gerado')).not.toBeInTheDocument()
    expect(screen.queryByText('Descrição gerada')).not.toBeInTheDocument()
    expect(screen.queryByText('gpt-test')).not.toBeInTheDocument()
  })

  it('classifies confidence at the requested boundaries', () => {
    expect(confidenceLabel(0)).toBe('Baixa')
    expect(confidenceLabel(0.49)).toBe('Baixa')
    expect(confidenceLabel(0.5)).toBe('Média')
    expect(confidenceLabel(0.79)).toBe('Média')
    expect(confidenceLabel(0.8)).toBe('Alta')
    expect(confidenceLabel(1)).toBe('Alta')
  })

  it('hides null confidence, empty pending information and the whole block without analysis', () => {
    const { rerender } = render(<RequestAiAssistant analysis={analysis({
      confidence: null,
      missingInformation: [],
    })} />)

    expect(screen.queryByText('Confiança da análise')).not.toBeInTheDocument()
    expect(screen.queryByText('Possíveis informações pendentes')).not.toBeInTheDocument()
    rerender(<RequestAiAssistant analysis={null} />)
    expect(screen.queryByText('Assistente Comvy')).not.toBeInTheDocument()
  })

  it('renders safely in a narrow viewport', () => {
    window.innerWidth = 360
    render(<RequestAiAssistant analysis={analysis()} />)
    expect(screen.getByText('Assistente Comvy')).toBeVisible()
  })

  it('keeps internal details hidden from residents and managers of another condominium', () => {
    expect(canViewInternalRequestDetails(false, false, 'condo-a', 'condo-a')).toBe(false)
    expect(canViewInternalRequestDetails(false, true, 'condo-a', 'condo-b')).toBe(false)
    expect(canViewInternalRequestDetails(false, true, 'condo-a', 'condo-a')).toBe(true)
    expect(canViewInternalRequestDetails(true, false, 'condo-a', null)).toBe(true)
  })
})

describe('OriginalReportAccordion', () => {
  it('starts collapsed and reveals the complete WhatsApp report and attachments', async () => {
    const user = userEvent.setup()
    const fullText = 'Relato original integral\ncom uma segunda linha.'
    render(<OriginalReportAccordion
      report={{ text: fullText, channel: 'WhatsApp', createdAt: '2026-07-31T12:00:00Z' }}
      attachments={<div>Anexos relacionados</div>}
    />)

    expect(screen.getByRole('button', { name: 'Relato original do morador' }))
      .toHaveAttribute('aria-expanded', 'false')
    await user.click(screen.getByRole('button', { name: 'Relato original do morador' }))
    expect(screen.getByText(/Relato original integral\s+com uma segunda linha\./)).toBeVisible()
    expect(screen.getByText('Origem: WhatsApp')).toBeVisible()
    expect(screen.getByText('Anexos relacionados')).toBeVisible()
  })
})
