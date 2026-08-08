import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { RequestTimeline } from './RequestTimeline'

describe('RequestTimeline', () => {
  it('shows spontaneous resident content beside its contextual event', () => {
    render(<RequestTimeline history={[]} messages={[{
      id: 'message', requestId: 'request',
      author: { id: 'resident', fullName: 'Maria', isManager: false },
      content: 'O porteiro informou que a TAG já chegou.',
      channel: 'WhatsAppResidentUpdate', createdAt: '2026-08-10T17:30:00Z',
    }]} />)

    expect(screen.getByText('Atualização do morador')).toBeVisible()
    expect(screen.getByText('O porteiro informou que a TAG já chegou.')).toBeVisible()
  })
})
