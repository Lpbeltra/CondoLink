import { fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { ThemeProvider, createTheme } from '@mui/material'
import { WhatsAppDeliveryIndicator } from './WhatsAppDeliveryIndicator'
import { RequestConversation } from './RequestConversation'
import { RequestTimeline } from './RequestTimeline'
import type { WhatsAppDelivery } from '../types'

const cases: [WhatsAppDelivery['status'], string, string][] = [
  ['Pending', 'Enviando pelo WhatsApp', 'ScheduleRoundedIcon'],
  ['Processing', 'Enviando pelo WhatsApp', 'ScheduleRoundedIcon'],
  ['Sent', 'Enviado pelo WhatsApp', 'CheckRoundedIcon'],
  ['Delivered', 'Entregue pelo WhatsApp', 'DoneAllRoundedIcon'],
  ['Read', 'Lido no WhatsApp', 'DoneAllRoundedIcon'],
  ['Failed', 'Falha no envio pelo WhatsApp', 'ErrorOutlineRoundedIcon'],
  ['PermanentlyFailed', 'Falha no envio pelo WhatsApp', 'ErrorOutlineRoundedIcon'],
  ['Skipped', 'Envio pelo WhatsApp não realizado', 'ErrorOutlineRoundedIcon'],
  ['Cancelled', 'Envio pelo WhatsApp cancelado', 'ErrorOutlineRoundedIcon'],
]

describe('WhatsApp delivery', () => {
  it.each(cases)('renders %s with an accessible tooltip', async (status, label, icon) => {
    render(<WhatsAppDeliveryIndicator delivery={{ status }} />)
    expect(screen.getByTestId(icon)).toBeVisible()
    await userEvent.setup().hover(screen.getByRole('img', { name: label }))
    expect(await screen.findByRole('tooltip')).toHaveTextContent(label)
  })

  it('opens the tooltip on touch', async () => {
    render(<WhatsAppDeliveryIndicator delivery={{ status: 'Sent' }} />)
    fireEvent.touchStart(screen.getByRole('img'))
    expect(await screen.findByRole('tooltip')).toHaveTextContent('Enviado pelo WhatsApp')
    fireEvent.touchEnd(screen.getByRole('img'))
  })

  it('opens the tooltip with keyboard focus', async () => {
    render(<WhatsAppDeliveryIndicator delivery={{ status: 'Sent' }} />)
    await userEvent.setup().tab()
    expect(await screen.findByRole('tooltip')).toHaveTextContent('Enviado pelo WhatsApp')
  })

  it.each(['light', 'dark'] as const)('uses theme colors in %s mode', mode => {
    const theme = createTheme({ palette: { mode } })
    render(<ThemeProvider theme={theme}><WhatsAppDeliveryIndicator delivery={{ status: 'Read' }} /></ThemeProvider>)
    expect(screen.getByRole('img')).toHaveStyle({ color: theme.palette.primary.main })
  })

  it('does not show an indicator without an associated outbound', () => {
    render(<RequestConversation requestId="request" status="InProgress" readOnly onMessageCreated={() => {}}
      messages={[{ id: 'message', requestId: 'request', author: { id: 'manager', fullName: 'Ana', isManager: true },
        content: 'Update', createdAt: '2026-09-01T12:00:00Z' }]} />)
    expect(screen.queryByRole('img')).not.toBeInTheDocument()
  })

  it('updates the conversation indicator when polling supplies a new status', () => {
    const conversation = (status: WhatsAppDelivery['status']) => <RequestConversation requestId="request" status="InProgress" readOnly onMessageCreated={() => {}}
      messages={[{ id: 'message', requestId: 'request', author: { id: 'manager', fullName: 'Ana', isManager: true },
        content: 'Update', createdAt: '2026-09-01T12:00:00Z', whatsAppDelivery: { status } }]} />
    const { rerender } = render(conversation('Pending'))
    expect(screen.getByRole('img', { name: 'Enviando pelo WhatsApp' })).toBeVisible()
    rerender(conversation('Delivered'))
    expect(screen.getByRole('img', { name: 'Entregue pelo WhatsApp' })).toBeVisible()
    rerender(conversation('Failed'))
    expect(screen.getByRole('img', { name: 'Falha no envio pelo WhatsApp' })).toBeVisible()
    expect(screen.getByText('Update')).toBeVisible()
  })

  it('shows delivery next to timeline metadata', () => {
    render(<RequestTimeline history={[{ id: 'history', previousStatus: 'Open', newStatus: 'InProgress',
      changedByUserId: 'manager', changedByFullName: 'Ana', reason: null,
      createdAt: '2026-09-01T12:00:00Z', whatsAppDelivery: { status: 'Sent' } }]} />)
    expect(screen.getByRole('img', { name: 'Enviado pelo WhatsApp' })).toBeVisible()
  })
})
