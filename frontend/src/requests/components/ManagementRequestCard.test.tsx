import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import { ManagementRequestCard } from './ManagementRequestCard'
import type { ManagementRequestItem } from '../types'

const request: ManagementRequestItem = {
  id: 'r1', condominiumId: 'c1', condominiumName: 'Residencial',
  author: { id: 'u1', fullName: 'Morador' },
  category: { id: 'cat1', name: 'Manutenção' }, targetUnit: null,
  title: 'Portão', status: 'InProgress', priority: 'Normal',
  createdAt: '2026-08-07T12:00:00Z', updatedAt: '2026-08-07T12:00:00Z',
  resolvedAt: null,
}

describe('ManagementRequestCard', () => {
  it('shows spontaneous resident updates as a badge separate from status', () => {
    render(<MemoryRouter><ManagementRequestCard request={{ ...request, hasUnreadResidentUpdate: true }} /></MemoryRouter>)

    expect(screen.getByText('Atualizado pelo morador')).toBeInTheDocument()
    expect(screen.getByText('Em andamento')).toBeInTheDocument()
    expect(screen.queryByText('Morador respondeu')).not.toBeInTheDocument()
  })

  it('does not show the badge without an unread update', () => {
    render(<MemoryRouter><ManagementRequestCard request={request} /></MemoryRouter>)

    expect(screen.queryByText('Atualizado pelo morador')).not.toBeInTheDocument()
  })
})