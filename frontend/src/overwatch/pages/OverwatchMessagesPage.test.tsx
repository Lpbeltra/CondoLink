import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import * as messages from '../messages'
import { OverwatchMessagesPage } from './OverwatchMessagesPage'

const template: messages.OperationalMessageTemplate = {
  key: 'InformationRequested',
  title: 'Solicitação de informação',
  description: 'Enviada quando o síndico pede mais detalhes ao morador.',
  prefix: 'Olá',
  suffix: 'Obrigado',
  structuralSuffix: '',
  dynamicContent: '{MensagemDoSindico}',
  mode: 'Template',
  modeLabel: 'Template Meta',
  metaTemplateName: 'info_requested',
  metaTemplateLanguage: 'pt_BR',
  metaQuickReplies: ['Ver atualização'],
  isOverride: true,
  updatedAt: '2026-08-18T12:00:00Z',
  updatedByUserId: 'user-123456',
  partMaximumLength: 300,
  outboundMaximumLength: 1000,
}

afterEach(() => vi.restoreAllMocks())

describe('OverwatchMessagesPage', () => {
  it('shows the customization summary for an overridden template', async () => {
    vi.spyOn(messages, 'listOperationalMessages').mockResolvedValue([template])
    render(<OverwatchMessagesPage />)

    expect(
      await screen.findByText(/Personalizada · atualizada em.*por user-123/),
    ).toBeInTheDocument()
  })

  it('shows a list-loading error on the page', async () => {
    vi.spyOn(messages, 'listOperationalMessages').mockRejectedValue(new Error('offline'))
    render(<OverwatchMessagesPage />)

    expect(
      await screen.findByText('Não foi possível concluir esta ação. Verifique os dados e tente novamente.'),
    ).toBeInTheDocument()
  })

  it('shows a save failure only inside the dialog, not as a list error', async () => {
    const user = userEvent.setup()
    vi.spyOn(messages, 'listOperationalMessages').mockResolvedValue([template])
    vi.spyOn(messages, 'updateOperationalMessage').mockRejectedValue({
      isAxiosError: true,
      response: { status: 409 },
    })
    render(<OverwatchMessagesPage />)

    await user.click(await screen.findByRole('button', { name: 'Editar' }))
    await user.click(screen.getByRole('button', { name: 'Salvar' }))

    expect(
      await screen.findByText('A operação não pôde ser concluída devido ao estado atual dos dados.'),
    ).toBeInTheDocument()
    expect(
      screen.queryByText('Não foi possível concluir esta ação. Verifique os dados e tente novamente.'),
    ).not.toBeInTheDocument()
  })

  it('saves changes and restores the official default', async () => {
    const user = userEvent.setup()
    vi.spyOn(messages, 'listOperationalMessages').mockResolvedValue([template])
    const updated = { ...template, prefix: 'Novo prefixo' }
    vi.spyOn(messages, 'updateOperationalMessage').mockResolvedValue(updated)
    vi.spyOn(messages, 'restoreOperationalMessage').mockResolvedValue({ ...template, isOverride: false })
    render(<OverwatchMessagesPage />)

    await user.click(await screen.findByRole('button', { name: 'Editar' }))
    await user.click(screen.getByRole('button', { name: 'Salvar' }))
    expect(await screen.findByText('Mensagem salva com sucesso.')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Restaurar padrão' }))
    expect(await screen.findByText('Padrão oficial restaurado.')).toBeInTheDocument()
  })
})
