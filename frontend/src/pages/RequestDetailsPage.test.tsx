import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { RequestDetailsPage } from './RequestDetailsPage'
import { createRequestMessage, getRequest, listRequestAttachments, listRequestMessages, listRequestServiceProviders } from '../requests/api'
import { listInternalNotes } from '../requests/internalNotes'
import type { RequestDetails } from '../requests/types'

vi.mock('../condominiums/CondominiumContext', () => ({ useCondominium: () => ({ currentCondominium: { condominium: { id: 'condo' } }, isManager: false }) }))
vi.mock('../requests/api', () => ({ getRequest: vi.fn(), listRequestMessages: vi.fn(), listRequestAttachments: vi.fn(), getRequestAttachmentBlob: vi.fn(), createRequestMessage: vi.fn(), listRequestServiceProviders: vi.fn(), setRequestServiceProvider: vi.fn(), prepareProviderContactMessage: vi.fn() }))
vi.mock('../requests/internalNotes', () => ({ listInternalNotes: vi.fn(), createInternalNote: vi.fn(), editInternalNote: vi.fn(), deleteInternalNote: vi.fn() }))
vi.mock('../requests/components/RequestManagementActions', () => ({ RequestManagementActions: () => <div>Ações de atendimento</div> }))
vi.mock('../requests/components/RequestAttachments', () => ({ RequestAttachments: () => <div>Anexos</div> }))

const details: RequestDetails = {
  id: 'request', condominiumId: 'condo', title: 'Portão', description: 'Relato bruto original',
  author: { id: 'resident', fullName: 'Maria' }, category: { id: 'category', name: 'Manutenção' },
  status: 'InProgress', priority: 'Normal', targetUnit: null, createdAt: '2026-09-07T12:00:00Z',
  updatedAt: '2026-09-07T12:00:00Z', resolvedAt: null, statusHistory: [],
  originalReport: null, residentReplyRequirement: null, canManageInternalNotes: true,
  aiAnalysis: { title: 'Portão', description: 'Resumo contextual atualizado', suggestedCategory: 'Segurança', confidence: .8,
    missingInformation: ['Mais informações'], generatedAt: '2026-09-07T12:00:00Z', model: 'test' },
}
function page(managementMode = true) {
  return render(<MemoryRouter initialEntries={['/requests/request']}><Routes>
    <Route path="/requests/:requestId" element={<RequestDetailsPage managementMode={managementMode} />} />
  </Routes></MemoryRouter>)
}
describe('RequestDetailsPage organization', () => {
  beforeEach(() => {
    vi.resetAllMocks()
    vi.mocked(getRequest).mockResolvedValue(details)
    vi.mocked(listRequestMessages).mockResolvedValue([])
    vi.mocked(listRequestAttachments).mockResolvedValue([])
    vi.mocked(listRequestServiceProviders).mockResolvedValue([])
    vi.mocked(listInternalNotes).mockResolvedValue([])
  })

  it('places the summary and suggested category in the main block and keeps the original report', async () => {
    page()
    const heading = await screen.findByRole('heading', { name: 'Resumo do atendimento' })
    const main = within(heading.parentElement!)
    expect(main.getByText('Resumo contextual atualizado')).toBeVisible()
    expect(main.getByText('Sugestão da IA: Segurança')).toBeVisible()
    expect(main.getByText(/Manutenção · aberta/)).toBeVisible()
    expect(screen.getAllByText('Resumo contextual atualizado')).toHaveLength(1)
    expect(screen.queryByRole('heading', { name: 'Assistente Comvy' })).not.toBeInTheDocument()
    expect(screen.queryByText('Confiança da análise')).not.toBeInTheDocument()
    expect(screen.queryByText('Mais informações')).not.toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Atualizações do atendimento' })).toBeVisible()
    expect(screen.queryByLabelText('Descreva uma nova informação sobre a solicitação')).not.toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Notas internas' })).toBeVisible()
    await userEvent.setup().click(screen.getByRole('button', { name: 'Relatos originais do morador' }))
    expect(await screen.findByText('Relato bruto original')).toBeVisible()
  })

  it.each([null, { ...details.aiAnalysis!, description: '   ' }])('falls back to the original description when there is no valid summary', async aiAnalysis => {
    vi.mocked(getRequest).mockResolvedValue({ ...details, aiAnalysis })
    page()
    const heading = await screen.findByRole('heading', { name: 'Resumo do atendimento' })
    expect(within(heading.parentElement!).getByText('Relato bruto original')).toBeVisible()
  })

  it('does not load internal notes for a resident, even on an administrative URL', async () => {
    vi.mocked(getRequest).mockResolvedValue({ ...details, aiAnalysis: null, canManageInternalNotes: false })
    page()
    await screen.findByRole('heading', { name: 'Resumo do atendimento' })
    expect(screen.queryByRole('heading', { name: 'Notas internas' })).not.toBeInTheDocument()
    expect(listInternalNotes).not.toHaveBeenCalled()
    expect(screen.queryByText('Ações de atendimento')).not.toBeInTheDocument()
  })

  it('allows an Attendance-authorized SubManager without requiring the Manager frontend flag', async () => {
    page(false)
    expect(await screen.findByRole('heading', { name: 'Notas internas' })).toBeVisible()
    expect(listInternalNotes).toHaveBeenCalledWith('request')
  })

  it('offers residents a deliberate update dialog instead of a permanent composer', async () => {
    vi.mocked(getRequest).mockResolvedValue({ ...details, canManageInternalNotes: false })
    vi.mocked(listRequestMessages).mockResolvedValue([])
    vi.mocked(createRequestMessage).mockResolvedValue({ id: 'new', requestId: 'request', author: { id: 'resident', fullName: 'Maria' }, content: 'Nova informação', channel: 'Portal', createdAt: '2026-09-07T12:30:00Z' })
    page(false)
    const user = userEvent.setup()
    await user.click(await screen.findByRole('button', { name: 'Enviar nova atualização' }))
    expect(screen.getByLabelText('Nova informação sobre o atendimento')).toBeVisible()
    await user.type(screen.getByLabelText('Nova informação sobre o atendimento'), 'Nova informação')
    await user.click(screen.getByRole('button', { name: 'Enviar atualização' }))
    expect(createRequestMessage).toHaveBeenCalledWith('request', 'Nova informação')
  })
})
