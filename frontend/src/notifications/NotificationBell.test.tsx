import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { NotificationBell } from './NotificationBell'
import type { AppNotification } from './types'

const listNotifications = vi.fn()
const markNotificationRead = vi.fn()
const markAllNotificationsRead = vi.fn()

vi.mock('./api', () => ({
  listNotifications: (...args: unknown[]) => listNotifications(...args),
  markNotificationRead: (...args: unknown[]) => markNotificationRead(...args),
  markAllNotificationsRead: (...args: unknown[]) => markAllNotificationsRead(...args),
  getUnreadCount: vi.fn(),
}))

const navigate = vi.fn()
vi.mock('react-router-dom', async () => ({
  ...(await vi.importActual<typeof import('react-router-dom')>('react-router-dom')),
  useNavigate: () => navigate,
}))

let isManager = false
vi.mock('../condominiums/CondominiumContext', () => ({
  useCondominium: () => ({ isManager }),
}))

function notification(overrides: Partial<AppNotification> = {}): AppNotification {
  return {
    id: 'n1',
    condominiumId: 'c1',
    type: 'RequestCreated',
    title: 'Nova solicitação',
    body: 'Manutenção: Vazamento',
    requestId: 'r1',
    createdAt: new Date().toISOString(),
    readAt: null,
    ...overrides,
  }
}

function renderBell() {
  return render(<MemoryRouter><NotificationBell /></MemoryRouter>)
}

describe('NotificationBell', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    isManager = false
    listNotifications.mockResolvedValue({ items: [], unreadCount: 0 })
    markNotificationRead.mockResolvedValue(undefined)
    markAllNotificationsRead.mockResolvedValue(1)
  })

  it('announces the unread count in the accessible name', async () => {
    listNotifications.mockResolvedValue({
      items: [notification()],
      unreadCount: 3,
    })
    renderBell()

    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Notificações, 3 não lidas' }))
        .toBeInTheDocument())
  })

  it('uses a plain label when nothing is unread', async () => {
    renderBell()
    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Notificações' })).toBeInTheDocument())
  })

  it('shows an empty state when there is nothing to read', async () => {
    const user = userEvent.setup()
    renderBell()
    await waitFor(() => expect(listNotifications).toHaveBeenCalled())

    await user.click(screen.getByRole('button'))

    expect(await screen.findByText('Nenhuma notificação por aqui.')).toBeInTheDocument()
  })

  it('lists notifications when opened', async () => {
    const user = userEvent.setup()
    listNotifications.mockResolvedValue({
      items: [notification({ title: 'Status atualizado' })],
      unreadCount: 1,
    })
    renderBell()
    await waitFor(() => expect(listNotifications).toHaveBeenCalled())

    await user.click(screen.getByRole('button'))

    expect(await screen.findByText('Status atualizado')).toBeInTheDocument()
  })

  it('marks a notification read and navigates to the request', async () => {
    const user = userEvent.setup()
    listNotifications.mockResolvedValue({
      items: [notification()],
      unreadCount: 1,
    })
    renderBell()
    await waitFor(() => expect(listNotifications).toHaveBeenCalled())

    await user.click(screen.getByRole('button'))
    await user.click(await screen.findByText('Nova solicitação'))

    await waitFor(() => expect(markNotificationRead).toHaveBeenCalledWith('n1'))
    expect(navigate).toHaveBeenCalledWith('/requests/r1')
  })

  it('routes managers to the management view of the request', async () => {
    const user = userEvent.setup()
    isManager = true
    listNotifications.mockResolvedValue({
      items: [notification()],
      unreadCount: 1,
    })
    renderBell()
    await waitFor(() => expect(listNotifications).toHaveBeenCalled())

    await user.click(screen.getByRole('button'))
    await user.click(await screen.findByText('Nova solicitação'))

    await waitFor(() =>
      expect(navigate).toHaveBeenCalledWith('/management/requests/r1'))
  })

  it('does not re-mark an already read notification', async () => {
    const user = userEvent.setup()
    listNotifications.mockResolvedValue({
      items: [notification({ readAt: new Date().toISOString() })],
      unreadCount: 0,
    })
    renderBell()
    await waitFor(() => expect(listNotifications).toHaveBeenCalled())

    await user.click(screen.getByRole('button'))
    await user.click(await screen.findByText('Nova solicitação'))

    expect(markNotificationRead).not.toHaveBeenCalled()
    expect(navigate).toHaveBeenCalledWith('/requests/r1')
  })

  it('disables "clear all" when nothing is unread', async () => {
    const user = userEvent.setup()
    listNotifications.mockResolvedValue({
      items: [notification({ readAt: new Date().toISOString() })],
      unreadCount: 0,
    })
    renderBell()
    await waitFor(() => expect(listNotifications).toHaveBeenCalled())

    await user.click(screen.getByRole('button'))
    await screen.findByText('Notificações')

    expect(screen.getByRole('button', { name: /Limpar todas/ })).toBeDisabled()
  })

  it('marks every notification read from the header action', async () => {
    const user = userEvent.setup()
    listNotifications.mockResolvedValue({
      items: [notification(), notification({ id: 'n2' })],
      unreadCount: 2,
    })
    renderBell()
    await waitFor(() => expect(listNotifications).toHaveBeenCalled())

    await user.click(screen.getByRole('button', { name: /Notificações/ }))
    await user.click(await screen.findByRole('button', { name: /Limpar todas/ }))

    await waitFor(() => expect(markAllNotificationsRead).toHaveBeenCalled())
    expect(await screen.findByText('Todas as notificações foram marcadas como lidas.')).toBeInTheDocument()
  })

  it('survives a failed poll without crashing the shell', async () => {
    listNotifications.mockRejectedValue(new Error('offline'))
    renderBell()

    await waitFor(() => expect(listNotifications).toHaveBeenCalled())
    expect(screen.getByRole('button', { name: 'Notificações' })).toBeInTheDocument()
  })
})
