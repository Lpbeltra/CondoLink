import { describe, expect, it } from 'vitest'
import { employeeCredentialsText } from './credentials'

describe('employee temporary credentials', () => {
  it('formats all data for secure sharing', () => {
    const text = employeeCredentialsText({
      id: 'employee-id',
      managementCompanyId: 'company-id',
      userId: 'user-id',
      fullName: 'Maria da Silva',
      email: 'maria@example.com',
      contact: null,
      jobTitle: 'Financeiro',
      isActive: true,
      temporaryPassword: 'Temporary1!',
    })

    expect(text).toContain('Maria da Silva')
    expect(text).toContain('maria@example.com')
    expect(text).toContain('Temporary1!')
  })
})
