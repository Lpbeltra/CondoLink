import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { ResidentSummaryCard } from './ResidentSummaryCard'

describe('ResidentSummaryCard', () => {
  it('shows the resident fields and formatted Brazilian phone', () => {
    render(<ResidentSummaryCard resident={{ fullName: 'Lívia Ávila',
      block: 'Torre A', unit: '101', phoneNumber: '+5511999990001',
      email: 'livia@example.com', relationship: 'Tenant' }} />)
    expect(screen.getByText('Dados do morador')).toBeInTheDocument()
    expect(screen.getByText('Lívia Ávila')).toBeInTheDocument()
    expect(screen.getByText('+55 (11) 99999-0001')).toBeInTheDocument()
    expect(screen.getByText('Inquilino')).toBeInTheDocument()
  })

  it('omits block and handles incomplete international data', () => {
    render(<ResidentSummaryCard resident={{ fullName: 'Alex Doe', block: null,
      unit: '7', phoneNumber: '+1 212 555 1234', email: null,
      relationship: null }} />)
    expect(screen.queryByText('Bloco')).not.toBeInTheDocument()
    expect(screen.getByText('+1 212 555 1234')).toBeInTheDocument()
    expect(screen.getByText('Não informado')).toBeInTheDocument()
  })
})
