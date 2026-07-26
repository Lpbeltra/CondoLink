import { describe, expect, it } from 'vitest'
import {
  managerCredentialsText, managerDetailsPath, upsertManager,
} from './presentation'

describe('manager presentation', () => {
  it('builds the details route', () => {
    expect(managerDetailsPath('manager-id')).toBe('/overwatch/managers/manager-id')
  })

  it('formats one-time credentials', () => {
    const text = managerCredentialsText({
      id: '1', fullName: 'Manager', email: 'manager@example.com',
      temporaryPassword: 'Temporary1!', isActive: true,
      condominiumCount: 0, createdAt: '', updatedAt: '',
    })
    expect(text).toContain('manager@example.com')
    expect(text).toContain('Temporary1!')
  })

  it('upserts and sorts managers', () => {
    const base = {
      email: 'x@example.com', isActive: true, condominiumCount: 0,
      createdAt: '', updatedAt: '',
    }
    const result = upsertManager(
      [{ ...base, id: '2', fullName: 'Zulu' }],
      { ...base, id: '1', fullName: 'Alpha' },
    )
    expect(result.map((item) => item.fullName)).toEqual(['Alpha', 'Zulu'])
  })
})
