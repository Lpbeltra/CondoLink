import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { OriginalReportAccordion } from './OriginalReportAccordion'
import { canViewInternalRequestDetails, confidenceLabel, RequestAiAssistant } from './RequestAiAssistant'
import { getRequestAttachmentBlob, listRequestAttachments } from '../api'
import type { RequestAiAnalysis, RequestMessage } from '../types'

vi.mock('../api', () => ({ getRequestAttachmentBlob: vi.fn(), listRequestAttachments: vi.fn() }))

const analysis = (overrides: Partial<RequestAiAnalysis> = {}): RequestAiAnalysis => ({
  title: 'Título gerado', description: 'Descrição gerada', suggestedCategory: 'Jardinagem',
  confidence: 0.82, missingInformation: ['Informar quando o problema começou'],
  generatedAt: '2026-07-31T12:00:00Z', model: 'gpt-test', ...overrides,
})

describe('RequestAiAssistant', () => {
  it('shows administrative insights without repeating generated content', () => {
    render(<RequestAiAssistant analysis={analysis()} />)
    expect(screen.getByText('Assistente Comvy')).toBeInTheDocument()
    expect(screen.getByText('Jardinagem')).toBeInTheDocument()
    expect(screen.getByText('Alta')).toBeInTheDocument()
    expect(screen.queryByText('Título gerado')).not.toBeInTheDocument()
    expect(screen.queryByText('gpt-test')).not.toBeInTheDocument()
  })

  it('classifies confidence at the requested boundaries', () => {
    expect(confidenceLabel(0.49)).toBe('Baixa')
    expect(confidenceLabel(0.5)).toBe('Média')
    expect(confidenceLabel(0.8)).toBe('Alta')
  })

  it('hides unavailable analysis details', () => {
    const { rerender } = render(<RequestAiAssistant analysis={analysis({ confidence: null, missingInformation: [] })} />)
    expect(screen.queryByText('Confiança da análise')).not.toBeInTheDocument()
    rerender(<RequestAiAssistant analysis={null} />)
    expect(screen.queryByText('Assistente Comvy')).not.toBeInTheDocument()
  })

  it('keeps internal details scoped to management', () => {
    expect(canViewInternalRequestDetails(false, false, 'condo-a', 'condo-a')).toBe(false)
    expect(canViewInternalRequestDetails(false, true, 'condo-a', 'condo-a')).toBe(true)
    expect(canViewInternalRequestDetails(true, false, 'condo-a', null)).toBe(true)
  })
})

describe('OriginalReportAccordion', () => {
  const messages: RequestMessage[] = [
    { id: 'opening', requestId: 'request-id', author: { id: 'resident', fullName: 'Maria' }, content: 'Relato de abertura', channel: 'WhatsApp', createdAt: '2026-08-01T10:00:00Z' },
    { id: 'text-update', requestId: 'request-id', author: { id: 'resident', fullName: 'Maria' }, content: 'Atualização textual', channel: 'WhatsAppResidentUpdate', createdAt: '2026-08-02T10:00:00Z' },
    { id: 'audio-old', requestId: 'request-id', author: { id: 'resident', fullName: 'Maria' }, content: 'Transcrição antiga', channel: 'WhatsAppResidentUpdate', createdAt: '2026-08-03T10:00:00Z' },
    { id: 'audio-new', requestId: 'request-id', author: { id: 'resident', fullName: 'Maria' }, content: 'Transcrição recente', channel: 'WhatsAppResidentUpdate', createdAt: '2026-08-04T10:00:00Z' },
  ]

  beforeEach(() => {
    vi.mocked(getRequestAttachmentBlob).mockResolvedValue(new Blob(['audio'], { type: 'audio/ogg' }))
    vi.mocked(listRequestAttachments).mockResolvedValue([
      { id: 'new', requestId: 'request-id', requestMessageId: 'audio-new', originalFileName: 'new.ogg', contentType: 'audio/ogg', fileSize: 3, uploadedBy: { id: 'resident', fullName: 'Maria' }, createdAt: '2026-08-04T10:00:00Z', contentUrl: '/request-attachments/new/content' },
      { id: 'old', requestId: 'request-id', requestMessageId: 'audio-old', originalFileName: 'old.ogg', contentType: 'audio/ogg', fileSize: 3, uploadedBy: { id: 'resident', fullName: 'Maria' }, createdAt: '2026-08-03T10:00:00Z', contentUrl: '/request-attachments/old/content' },
    ])
    Object.defineProperty(URL, 'createObjectURL', { configurable: true, value: vi.fn(() => 'blob:audio') })
    Object.defineProperty(URL, 'revokeObjectURL', { configurable: true, value: vi.fn() })
  })

  it('separates chronological textual reports from newest-first audio without duplication', async () => {
    const user = userEvent.setup()
    render(<OriginalReportAccordion requestId="request-id" report={{ text: 'Relato de abertura', channel: 'WhatsApp', createdAt: '2026-08-01T10:00:00Z', audioAttachment: null }} messages={messages} authorId="resident" portalDescription="Descrição tratada" requestCreatedAt="2026-08-01T10:00:00Z" />)
    await user.click(screen.getByRole('button', { name: 'Relatos originais do morador' }))
    expect(await screen.findByText('Relato de abertura')).toBeVisible()
    expect(screen.getByText('Atualização textual')).toBeVisible()
    const players = await screen.findAllByLabelText(/Áudio do morador/)
    expect(players[0].getAttribute('aria-label')).toContain('04/08/2026')
    expect(screen.getAllByText('Transcrição recente')).toHaveLength(1)
    expect(screen.getAllByText('Transcrição antiga')).toHaveLength(1)
  })

  it('uses portal description as opening report when no WhatsApp original exists', async () => {
    const user = userEvent.setup()
    vi.mocked(listRequestAttachments).mockResolvedValue([])
    render(<OriginalReportAccordion requestId="request-id" report={null} messages={[]} authorId="resident" portalDescription="Abertura pelo portal" requestCreatedAt="2026-08-01T10:00:00Z" />)
    await user.click(screen.getByRole('button', { name: 'Relatos originais do morador' }))
    expect(screen.getByText('Abertura pelo portal')).toBeVisible()
    expect(screen.getByText('Origem: Portal')).toBeVisible()
  })
})
